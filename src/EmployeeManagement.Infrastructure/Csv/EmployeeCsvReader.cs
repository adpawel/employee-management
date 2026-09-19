using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using EmployeeManagement.Application.Employees;

namespace EmployeeManagement.Infrastructure.Csv;

// Only the file structure is checked here. Values are read as raw strings and validated per row by
// EmployeeService, so JSON and CSV input go through exactly the same rules.
internal sealed class EmployeeCsvReader : IEmployeeCsvReader
{
    private static readonly string[] RequiredColumns =
    [
        nameof(EmployeeRequest.Name),
        nameof(EmployeeRequest.HireDate),
        nameof(EmployeeRequest.Email),
        nameof(EmployeeRequest.PhoneNo),
        nameof(EmployeeRequest.ProfilePicture),
        nameof(EmployeeRequest.Status),
        nameof(EmployeeRequest.Address),
        nameof(EmployeeRequest.State),
        nameof(EmployeeRequest.Country),
        nameof(EmployeeRequest.City),
        nameof(EmployeeRequest.Pincode)
    ];

    private static readonly CsvConfiguration Configuration = new(CultureInfo.InvariantCulture)
    {
        PrepareHeaderForMatch = args => args.Header.Trim().ToLowerInvariant()
    };

    private static readonly Encoding StrictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public async Task<IReadOnlyList<EmployeeImportRow>> ReadAsync(Stream csv, CancellationToken ct)
    {
        using var reader = new StreamReader(csv, StrictUtf8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        using var csvReader = new CsvReader(reader, Configuration);

        try
        {
            if (!await csvReader.ReadAsync() || !csvReader.ReadHeader())
            {
                throw EmployeeImportFileError.Create("The file is empty.");
            }

            EnsureRequiredColumns(csvReader.HeaderRecord!);

            var rows = new List<EmployeeImportRow>();
            while (await csvReader.ReadAsync())
            {
                ct.ThrowIfCancellationRequested();

                rows.Add(new EmployeeImportRow(csvReader.Parser.Row, ReadRequest(csvReader)));

                if (rows.Count > EmployeeService.MaxImportRows)
                {
                    break;
                }
            }

            return rows;
        }
        catch (CsvHelperException ex)
        {
            var line = ex.Context?.Parser?.RawRow;
            throw EmployeeImportFileError.Create(line is > 0
                ? $"The file is not a valid CSV file (problem near line {line})."
                : "The file is not a valid CSV file.");
        }
        catch (DecoderFallbackException)
        {
            throw EmployeeImportFileError.Create("The file is not valid UTF-8 text.");
        }
    }

    private static void EnsureRequiredColumns(string[] header)
    {
        var present = header.Select(h => h.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = RequiredColumns.Where(c => !present.Contains(c)).ToList();

        if (missing.Count > 0)
        {
            throw EmployeeImportFileError.Create($"Missing required column(s): {string.Join(", ", missing)}.");
        }
    }

    private static EmployeeRequest ReadRequest(CsvReader csv) => new(
        Name: csv.GetField(nameof(EmployeeRequest.Name)),
        HireDate: csv.GetField(nameof(EmployeeRequest.HireDate)),
        Email: csv.GetField(nameof(EmployeeRequest.Email)),
        PhoneNo: csv.GetField(nameof(EmployeeRequest.PhoneNo)),
        ProfilePicture: csv.GetField(nameof(EmployeeRequest.ProfilePicture)),
        Status: csv.GetField(nameof(EmployeeRequest.Status)),
        Address: csv.GetField(nameof(EmployeeRequest.Address)),
        State: csv.GetField(nameof(EmployeeRequest.State)),
        Country: csv.GetField(nameof(EmployeeRequest.Country)),
        City: csv.GetField(nameof(EmployeeRequest.City)),
        Pincode: csv.GetField(nameof(EmployeeRequest.Pincode)));
}
