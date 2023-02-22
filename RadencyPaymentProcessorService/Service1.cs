using Microsoft.VisualBasic.FileIO;
using RadencyPaymentProcessorService.Models.Input;
using RadencyPaymentProcessorService.Models.Output;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq.Expressions;

namespace RadencyPaymentProcessorService
{
    public partial class Service1 : ServiceBase
    {
        private string input_source_path = string.Empty;
        private string output_source_path = string.Empty;
        private string date_format = "yyyy-MM-dd";
        private List<string> logger = new List<string>();
        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            this.ReadConfiguration();
            List<SourceRecord> sourceRecords = new List<SourceRecord>();
            foreach (string csv in this.GetCsvFiles())
            {
                string filename = $@"{input_source_path}\{csv}";
                List<SourceRecord> csvSourceRecords = this.ParseSource(filename, true);
                logger.Add($"In \"{filename}\" are {csvSourceRecords.Count} elements");
                sourceRecords.AddRange(csvSourceRecords);
            }
            foreach (string txt in this.GetTxtFiles())
            {
                string filename = $@"{input_source_path}\{txt}";
                List<SourceRecord> txtSourceRecords = this.ParseSource(filename, false);
                logger.Add($"In \"{filename}\" are {txtSourceRecords.Count} elements");
                sourceRecords.AddRange(txtSourceRecords);
            }
            // Converting
            List<CityModel> cities = new List<CityModel>();
            foreach (string cityName in
                sourceRecords
                .Select(x => x.Address)
                .Distinct())
            {
                CityModel city = new CityModel
                {
                    City = cityName,
                    Services = new List<Service>()
                };
                foreach (string serviceName in
                    sourceRecords
                    .Where(x => cityName == x.Address)
                    .Select(x => x.Service)
                    .Distinct())
                {
                    Service service = new Service
                    {
                        Name = serviceName,
                        Payers = new List<Payer>()
                    };
                    foreach (var user in
                        sourceRecords
                        .Where(x => x.Address == cityName && x.Service == serviceName)
                        .Select(x => new { x.Last_name, x.First_name, x.Payment, x.Account_number, x.Date }))
                    {
                        Payer payer = new Payer
                        {
                            Name = String.Join(" ", user.Last_name, user.First_name),
                            Account_number = user.Account_number,
                            Date = user.Date,
                            Payment = user.Payment
                        };
                        service.Payers.Add(payer);
                    }
                    city.Services.Add(service);
                }
                cities.Add(city);
            }
            // Serialization
            try
            {
                JsonSerializerOptions options= new JsonSerializerOptions {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                };
                options.Converters.Add(new Util.JsonDateConverter(date_format));
                string jsonResult = JsonSerializer.Serialize<List<CityModel>>(cities,options);
                logger.Add(jsonResult);
            }
            catch(Exception ex)
            {
                ReportError(ex);
            }
            // Saving meta.log
            this.SaveSourceRecords(sourceRecords);
        }

        protected override void OnStop()
        {
            // TODO: Save my log
        }
        private void ReadConfiguration()
        {
            input_source_path = ConfigurationManager.AppSettings["SourcePath"];
            output_source_path = ConfigurationManager.AppSettings["ProcessedDataPath"];
            date_format = ConfigurationManager.AppSettings["DateFormat"];
        }
        private void ReportError(Exception exception, string comment = "", bool showDetails = true)
        {
            string log = String.Join(" | ", DateTime.Now, comment);
            if (showDetails)
                log = String.Join(" | ", log, exception.Source, exception.Message, exception.ToString());
            logger.Add(log);
        }
        private List<SourceRecord> ParseSource(string filePath, bool csvMode = false)
        {
            List<SourceRecord> records = new List<SourceRecord>();
            try
            {
                using (TextFieldParser textFieldParser = new TextFieldParser(filePath))
                {
                    textFieldParser.TextFieldType = FieldType.Delimited;
                    textFieldParser.SetDelimiters(",");
                    textFieldParser.HasFieldsEnclosedInQuotes = true;

                    bool firstLine = csvMode;
                    while (!textFieldParser.EndOfData)
                    {
                        if (firstLine)
                        {
                            firstLine = false;
                            continue;
                        }
                        string[] columns = textFieldParser.ReadFields();
                        try
                        {
                            if (columns.Count() == 7)
                            {
                                // Checking fields are missing
                                foreach (string column in columns)
                                {
                                    if (String.IsNullOrEmpty(column))
                                    {
                                        throw new ArgumentNullException();
                                    }
                                }
                                SourceRecord sourceRecord = new SourceRecord();
                                // Strings
                                sourceRecord.First_name = columns[0];
                                sourceRecord.Last_name = columns[1];
                                sourceRecord.Address = columns[2];
                                sourceRecord.Service = columns[6];
                                // Parsing payment info
                                sourceRecord.Payment = Decimal.Parse(
                                    columns[3],
                                     NumberStyles.Any, CultureInfo.InvariantCulture);
                                // Extracting data
                                sourceRecord.Date = DateTime.ParseExact(
                                    columns[4],
                                    date_format,
                                    System.Globalization.CultureInfo.InvariantCulture);
                                // Parsing acc num info
                                sourceRecord.Account_number = long.Parse(columns[5]);
                                // Retrieving city from address
                                sourceRecord.Address = (columns[2].IndexOf(',') > -1) ?
                                    columns[2].Split(',')[0] : columns[2];
                                // Adding parced record
                                records.Add(sourceRecord);
                            }
                            else
                            {
                                throw new ArgumentException("There are not default arguments amount in the row");
                            }
                        }
                        catch (Exception ex)
                        {
                            ReportError(ex, $"Exception at parsing file \"{filePath}\"", false);
                        }
                    }
                }
            }
            catch (Microsoft.VisualBasic.FileIO.MalformedLineException ex)
            {
                ReportError(ex, $"Attempting to open a broken file \"{filePath}\"", false);
            }
            return records;
        }

        private List<string> GetCsvFiles()
        {
            DirectoryInfo inputSourcePath = new DirectoryInfo($@"{input_source_path}");
            List<string> csv_Files = inputSourcePath.GetFiles("*.csv")
                           .Where(file => file.Name.EndsWith(".csv"))
                           .Select(file => file.Name).ToList();
            return csv_Files;
        }
        private List<string> GetTxtFiles()
        {
            DirectoryInfo inputSourcePath = new DirectoryInfo($@"{input_source_path}");
            List<string> txt_Files = inputSourcePath.GetFiles("*.txt")
                           .Where(file => file.Name.EndsWith(".txt"))
                           .Select(file => file.Name).ToList();
            return txt_Files;
        }
        private void SaveSourceRecords(List<SourceRecord> sourceRecords)
        {
            // Current operation dir
            string currentOutputPath = $@"{output_source_path}\{DateTime.Now.ToString(date_format)}";
            string logFileName = "meta.log";
            if (!Directory.Exists(currentOutputPath))
            {
                Directory.CreateDirectory(currentOutputPath);
            }

            using (TextWriter tw = new StreamWriter(String.Join("/", currentOutputPath, logFileName)))
            {
                tw.WriteLine($"Service started at {DateTime.Now.ToShortTimeString()} Rows: {sourceRecords.Count}");
                foreach (string log in this.logger)
                {
                    tw.WriteLine(log);
                }
                foreach (SourceRecord sr in sourceRecords)
                {
                    tw.WriteLine(sr);
                }
                tw.Close();
            }
        }
    }
}
