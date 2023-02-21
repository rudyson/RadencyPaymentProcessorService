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
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

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
            foreach(string csv in this.GetCsvFiles())
            {
                string filename = $@"{input_source_path}\{csv}";
                List<SourceRecord> csvSourceRecords = this.ParseSource(filename, true);
                logger.Add($"In \"{filename}\" are {csvSourceRecords.Count} elements");
                sourceRecords.Concat(csvSourceRecords);
            }
            foreach (string txt in this.GetTxtFiles())
            {
                string filename = $@"{input_source_path}\{txt}";
                List<SourceRecord> txtSourceRecords = this.ParseSource(filename, false);
                logger.Add($"In \"{filename}\" are {txtSourceRecords.Count} elements");
                sourceRecords.Concat(txtSourceRecords);
            }
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
        }
        private void ReportError(Exception exception, string comment="", bool showDetails = true) {
            string log = String.Join(" | ", DateTime.Now, comment);
            if (showDetails)
                log = String.Join(" | ", log, exception.Source,exception.Message, exception.ToString());
            logger.Add(log);
        }
        private List<SourceRecord> ParseSource(string filePath,bool csvMode = false)
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
                            if (columns.Count()==7)
                            {
                                SourceRecord sourceRecord = new SourceRecord();
                                // Strings
                                sourceRecord.First_Name = columns[0];
                                sourceRecord.Last_Name = columns[1];
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
                                sourceRecord.Payment = long.Parse(columns[5]);
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
                            ReportError(ex, $"Exception at parsing file \"{filePath}\"");
                        }
                    }
                }
            }
            catch (Microsoft.VisualBasic.FileIO.MalformedLineException ex)
            {
                ReportError(ex, $"Attempting to open a broken file \"{filePath}\"");
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
