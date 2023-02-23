using Microsoft.VisualBasic.FileIO;
using RadencyPaymentProcessorService.Models.Input;
using RadencyPaymentProcessorService.Models.Output;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text.Json;
using System.Threading;

namespace RadencyPaymentProcessorService
{
    public partial class Service1 : ServiceBase
    {
        private string input_source_path = string.Empty;
        private string output_source_path = string.Empty;
        private string date_format = "yyyy-MM-dd";
		private List<string> logger = new List<string>();
        private int output_file_num = -1;
        // Telemetry for meta.log
        private int telemetry_parsed_files = 0;
        private int telemetry_parsed_lines = 0;
        private int telemetry_found_errors = 0;
        private HashSet<string> telemetry_invalid_files = new HashSet<string>();
        // Schedulers
        private Timer callProcessing;
        private Timer midnightLogSaver;
        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            // Reading configuration from App.config
            this.ReadConfiguration();
			// Start processing of files in input every 5 minutes
			callProcessing = new Timer(
                StartProcessing,null,
                TimeSpan.Zero,
                TimeSpan.FromMinutes(5));
			// Saving logs in the end of the day
			TimeSpan timeUntilMidnight = DateTime.Now.Date.AddDays(1) - DateTime.Now;
			midnightLogSaver = new Timer(
                SaveLogs, null,
				DateTime.Now.Date.AddDays(1) - DateTime.Now,
                TimeSpan.FromDays(1));

		}

        protected override void OnStop()
        {
			callProcessing.Dispose();
			midnightLogSaver.Dispose();
            // Saving logs
            SaveLogs(null);
        }
        private void ReadConfiguration()
        {
            input_source_path = ConfigurationManager.AppSettings["SourcePath"];
            output_source_path = ConfigurationManager.AppSettings["ProcessedDataPath"];
            date_format = ConfigurationManager.AppSettings["DateFormat"];
		}
        private void SaveLogs(object state)
        {
			// Saving logs
			SaveMetaLog(String.Join("/", GetTodaysDirectoryPath(), "meta.log"));
			SaveErrorLog(String.Join("/", GetTodaysDirectoryPath(), "error.log"));
		}
        private void StartProcessing(object state)
        {
			// Processing
			List<SourceRecord> sourceRecords = new List<SourceRecord>();
			Dictionary<string, bool> processingQueue = new Dictionary<string, bool>();
			// Getting list of csv files
			foreach (string csvPath in this.GetCsvFiles())
			{
				processingQueue.Add($@"{input_source_path}\{csvPath}", true);
			}
			// Getting list of txt files
			foreach (string txtPath in this.GetTxtFiles())
			{
				processingQueue.Add($@"{input_source_path}\{txtPath}", false);
			}
			// Processing files
			foreach (KeyValuePair<string, bool> kvp in processingQueue)
			{
				// Processing each file in thread
				Thread fileProcessingThread = new Thread(() =>
				{
					// Reading Input *.txt or *.csv to SourceRecord model list
					List<SourceRecord> readSourceRecords = this.ParseSource(kvp.Key, kvp.Value);
					// Transforming SourceRecord list to CityModel list
					List<CityModel> transformedCities = this.TransfromSourceData(readSourceRecords);
					// Exporting CityModel list to json string
					string jsonCities = this.SerializeCitiesToJson(transformedCities);
					// Saving json
					SaveJson(String.Join("/", GetTodaysDirectoryPath(), $"output{CurrentOutputNumber()}.json"), jsonCities);
					try
					{
						File.Delete(kvp.Key);
					}
					catch (Exception ex)
					{
						ReportError(ex, "Deleting file from input directory");
					}
				});
				fileProcessingThread.Start();
			}
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
            telemetry_parsed_files++;
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
                        telemetry_parsed_lines++;
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
                            telemetry_found_errors++;
                            telemetry_invalid_files.Add(filePath);
                            ReportError(ex, $"Exception at parsing file \"{filePath}\"", false);
                        }
                    }
                }
            }
            catch (Microsoft.VisualBasic.FileIO.MalformedLineException ex)
            {
                telemetry_found_errors++;
                telemetry_invalid_files.Add(filePath);
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
        private int CurrentOutputNumber()
        {
            if (output_file_num < 0)
            {
                DirectoryInfo outputSourcePath = new DirectoryInfo(this.GetTodaysDirectoryPath());
                List<string> outputFiles = outputSourcePath.GetFiles("output*.json")
                               .Where(file => file.Name.EndsWith(".json"))
                               .Select(file => file.Name).ToList();
                if (outputFiles.Count > 0)
                {
                    int maxNum = output_file_num;
                    foreach (string file in outputFiles)
                    {
                        try
                        {
                            int curNum = Int32.Parse(file.Substring(6, file.Length - 11));
                            if (maxNum < curNum)
                            {
                                maxNum = curNum;
                            }
                        }
                        catch (Exception ex)
                        {
                            ReportError(ex, "Output number parsing error");
                        }
                    }
                    output_file_num = maxNum + 1;
                }
                else
                {
                    output_file_num = 1;
                }
                return output_file_num;
            }
            else
            {
                return ++output_file_num;
            }
        }
        private List<CityModel> TransfromSourceData(List<SourceRecord> sourceRecords)
        {
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
            return cities;
        }
        private string SerializeCitiesToJson(List<CityModel> cities, bool prettify = true)
        {
            string jsonResult = string.Empty;
            try
            {
                JsonSerializerOptions options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = prettify
                };
                options.Converters.Add(new Util.JsonDateConverter(date_format));
                jsonResult = JsonSerializer.Serialize<List<CityModel>>(cities, options);
            }
            catch (Exception ex)
            {
                ReportError(ex);
            }
            return jsonResult;
        }
        private void SaveMetaLog(string path, bool reset = false)
        {
            if (reset)
            {
                telemetry_parsed_files = 0;
                telemetry_parsed_lines = 0;
                telemetry_found_errors = 0;
                telemetry_invalid_files.Clear();
            }
            string log = String.Join(
                Environment.NewLine,
                $"parsed_files: {telemetry_parsed_files}",
                $"parsed_lines: {telemetry_parsed_lines}",
                $"found_errors: {telemetry_found_errors}",
                $"invalid_files: [\"{String.Join("\", \"", telemetry_invalid_files)}\"]");
            using (TextWriter tw = new StreamWriter(path))
            {
                tw.WriteLine(log);
                tw.Close();
            }
        }
        private void SaveErrorLog(string path)
        {
            using (TextWriter tw = new StreamWriter(path))
            {
                tw.WriteLine(String.Join(Environment.NewLine,this.logger));
                tw.Close();
            }
        }
        private void SaveJson(string path, string jsonData)
        {
            using (TextWriter tw = new StreamWriter(path))
            {
                tw.WriteLine(jsonData);
                tw.Close();
            }
        }
        private string GetTodaysDirectoryPath()
        {
            DateTime today = DateTime.Now;
			string path = String.Join("/", $@"{output_source_path}", $"{today.Day:00}-{today.Month:00}-{today.Year:0000}");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return path;
        }
    }
}