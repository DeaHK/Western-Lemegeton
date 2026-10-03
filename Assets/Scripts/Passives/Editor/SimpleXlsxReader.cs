#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using WesternLemegeton.Passives;

namespace WesternLemegeton.Passives.Editor
{
    public static class SimpleXlsxReader
    {
        public sealed class SheetData
        {
            public string Name;
            public List<string> Headers = new();
            public List<RowData> Rows = new();
        }

        public sealed class RowData
        {
            public int RowNumber;
            public Dictionary<string, string> Cells =
                new(StringComparer.OrdinalIgnoreCase);
        }

        public static SheetData ReadSheet(string filePath, string preferredSheetName) =>
            ReadSheetInternal(filePath, preferredSheetName, false);

        public static SheetData ReadRequiredSheet(string filePath, string requiredSheetName)
        {
            if (string.IsNullOrWhiteSpace(requiredSheetName))
                throw new ArgumentException("A required sheet name must be supplied.", nameof(requiredSheetName));
            return ReadSheetInternal(filePath, requiredSheetName, true);
        }

        private static SheetData ReadSheetInternal(string filePath, string preferredSheetName, bool requireExactSheet)
        {
            using FileStream stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using ZipArchive archive = new(stream, ZipArchiveMode.Read);

            XDocument workbook = LoadXml(archive, "xl/workbook.xml");
            XDocument rels = LoadXml(archive, "xl/_rels/workbook.xml.rels");

            XNamespace mainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XNamespace relNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            XNamespace packageRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";

            List<XElement> sheetElements = workbook
                .Descendants(mainNs + "sheet")
                .ToList();

            if (sheetElements.Count == 0)
            {
                throw new InvalidDataException("Workbook contains no sheets.");
            }

            XElement selectedSheet = sheetElements.FirstOrDefault(
                s => string.Equals(
                    (string)s.Attribute("name"),
                    preferredSheetName,
                    requireExactSheet ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase
                )
            );

            if (selectedSheet == null)
            {
                if (requireExactSheet)
                    throw new InvalidDataException($"Required worksheet '{preferredSheetName}' was not found.");
                selectedSheet = sheetElements[0];
            }

            string relationshipId = (string)selectedSheet.Attribute(relNs + "id");

            XElement relationship = rels
                .Descendants(packageRelNs + "Relationship")
                .FirstOrDefault(r =>
                    string.Equals(
                        (string)r.Attribute("Id"),
                        relationshipId,
                        StringComparison.Ordinal
                    )
                );

            if (relationship == null)
            {
                throw new InvalidDataException(
                    $"Could not resolve worksheet relationship '{relationshipId}'."
                );
            }

            string target = (string)relationship.Attribute("Target");
            string sheetEntryPath = ResolveWorkbookTarget(target);
            XDocument worksheet = LoadXml(archive, sheetEntryPath);

            List<string> sharedStrings = ReadSharedStrings(archive);
            Dictionary<int, string> headers = new();
            SheetData result = new()
            {
                Name = (string)selectedSheet.Attribute("name") ?? preferredSheetName
            };

            List<XElement> rowElements = worksheet
                .Descendants(mainNs + "row")
                .ToList();

            if (rowElements.Count == 0)
            {
                return result;
            }

            XElement headerRow = rowElements[0];

            foreach (XElement cell in headerRow.Elements(mainNs + "c"))
            {
                int columnIndex = GetColumnIndex((string)cell.Attribute("r"));
                string value = ReadCellValue(cell, mainNs, sharedStrings)?.Trim();

                if (!string.IsNullOrWhiteSpace(value))
                {
                    headers[columnIndex] = value;
                }
            }

            result.Headers = headers.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToList();

            foreach (XElement row in rowElements.Skip(1))
            {
                RowData rowData = new()
                {
                    RowNumber = ParseRowNumber((string)row.Attribute("r"))
                };

                bool hasAnyValue = false;

                foreach (XElement cell in row.Elements(mainNs + "c"))
                {
                    int columnIndex = GetColumnIndex((string)cell.Attribute("r"));

                    if (!headers.TryGetValue(columnIndex, out string header))
                    {
                        continue;
                    }

                    string value = ReadCellValue(cell, mainNs, sharedStrings);
                    rowData.Cells[header] = value;

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        hasAnyValue = true;
                    }
                }

                if (hasAnyValue)
                {
                    result.Rows.Add(rowData);
                }
            }

            return result;
        }

        public static bool TryParseFloat(string text, out float value)
        {
            return float.TryParse(
                text,
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out value
            );
        }

        private static List<string> ReadSharedStrings(ZipArchive archive)
        {
            ZipArchiveEntry entry = archive.GetEntry("xl/sharedStrings.xml");

            if (entry == null)
            {
                return new List<string>();
            }

            using Stream stream = entry.Open();
            XDocument document = XDocument.Load(stream);
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

            return document
                .Descendants(ns + "si")
                .Select(si => string.Concat(si.Descendants(ns + "t").Select(t => t.Value)))
                .ToList();
        }

        private static string ReadCellValue(
            XElement cell,
            XNamespace ns,
            IReadOnlyList<string> sharedStrings)
        {
            string type = (string)cell.Attribute("t");

            if (type == "inlineStr")
            {
                return string.Concat(cell.Descendants(ns + "t").Select(t => t.Value));
            }

            string raw = cell.Element(ns + "v")?.Value ?? string.Empty;

            if (type == "s" &&
                int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) &&
                index >= 0 &&
                index < sharedStrings.Count)
            {
                return sharedStrings[index];
            }

            if (type == "b")
            {
                return raw == "1" ? "TRUE" : "FALSE";
            }

            return raw;
        }

        private static XDocument LoadXml(ZipArchive archive, string entryPath)
        {
            ZipArchiveEntry entry = archive.GetEntry(entryPath);

            if (entry == null)
            {
                throw new InvalidDataException($"Missing xlsx entry: {entryPath}");
            }

            using Stream stream = entry.Open();
            return XDocument.Load(stream);
        }

        private static string ResolveWorkbookTarget(string target)
        {
            if (string.IsNullOrWhiteSpace(target))
            {
                throw new InvalidDataException("Worksheet target is empty.");
            }

            string normalized = target.Replace('\\', '/');

            if (normalized.StartsWith("/", StringComparison.Ordinal))
            {
                normalized = normalized.TrimStart('/');
            }
            else if (!normalized.StartsWith("xl/", StringComparison.OrdinalIgnoreCase))
            {
                normalized = "xl/" + normalized;
            }

            while (normalized.Contains("../"))
            {
                normalized = normalized.Replace("../", string.Empty);
            }

            return normalized;
        }

        private static int ParseRowNumber(string cellReference)
        {
            if (string.IsNullOrWhiteSpace(cellReference))
            {
                return 0;
            }

            string digits = new(cellReference.Where(char.IsDigit).ToArray());

            return int.TryParse(
                digits,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int rowNumber
            )
                ? rowNumber
                : 0;
        }

        private static int GetColumnIndex(string cellReference)
        {
            if (string.IsNullOrWhiteSpace(cellReference))
            {
                return -1;
            }

            int result = 0;

            foreach (char c in cellReference)
            {
                if (!char.IsLetter(c))
                {
                    break;
                }

                result = result * 26 + (char.ToUpperInvariant(c) - 'A' + 1);
            }

            return result - 1;
        }
    }
}
#endif
