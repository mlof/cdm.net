using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Octokit;
using Serilog;
using Serilog.Events;
using ShellProgressBar;
using NJsonSchema;
using NJsonSchema.CodeGeneration.CSharp;

namespace cdm.net.generator
{
    class Program
    {
        public async static Task Main(string[] args)
        {
            var app = new CommandLineApplication
            {
                FullName = "CDM.net Generator"
            };

            var verboseOption = app.Option("-v|--verbose", "Show verbose output", CommandOptionType.NoValue);
            var tagOption = app.Option("-t|--tag", "What tag to generate code for", CommandOptionType.SingleOrNoValue);
            string workingDirectory = Path.Join(Path.GetTempPath(), "cdm");

            app.HelpOption();

            app.OnExecute(() =>
            {
                var logger = new LoggerConfiguration();
                if (verboseOption.HasValue())
                {
                    logger.MinimumLevel.Is(LogEventLevel.Verbose);
                }

                logger.WriteTo.Console();

                Log.Logger = logger.CreateLogger();
                var cdmLocationPath = DownloadCdmZip(workingDirectory);
                ExtractZip(cdmLocationPath, workingDirectory);

                var outputDirectory = Path.Join(workingDirectory, "output");
                Directory.CreateDirectory(outputDirectory);
                ParseSchema(workingDirectory, outputDirectory);
                return 0;
            });

            app.Execute(args);
        }

        private static void ParseSchema(string workingDirectory, string OutputDirectory)
        {
            var schemaDocumentDirectory = Path.Join(workingDirectory, "CDM-master", "schemaDocuments");


            var schema = ParseSchemaRecursively(schemaDocumentDirectory).ToList();


            var x = 9;
            //CreateFileForSchema(Path.Join(workingDirectory, "output", "schema.cs"), schema);
        }

        private static IEnumerable<JsonSchema4> ParseSchemaRecursively(string path)
        {
            foreach (var filePath in Directory.GetFiles(path).Where(s => s.EndsWith(".cdm.json")))
            {
                Log.Information("Parsing {Path}", filePath);


                JsonSchema4 schema = null;
                try
                
                {
                    schema = JsonSchema4.FromFileAsync(filePath).GetAwaiter().GetResult();
                }
                catch (Exception e)
                {
                    Log.Error(e, "Could not parse {Path}", filePath);
                }

                if (schema != null)
                {
                    yield return schema;
                }
            }

            foreach (var directoryPath in Directory.GetDirectories(path))
            {
                foreach (var schema in ParseSchemaRecursively(directoryPath))
                {
                    yield return schema;
                }
            }
        }

        private static void CreateFileForSchema(string Path, JsonSchema4 schema)
        {
            using (var f = File.CreateText(Path))
            {
                var settings = new CSharpGeneratorSettings();
                var generator = new CSharpGenerator(schema, settings);
                var code = generator.GenerateFile(Path);
                var s = generator.GenerateFile();
                f.Write(s);
            }
        }


        private static void ExtractZip(string cdmLocationPath, string workingDirectory)
        {
            Log.Information("Extracting CDM ZIP");

            ZipFile.ExtractToDirectory(cdmLocationPath, workingDirectory);
            Log.Information("Finished Extracting");
        }

        private static string DownloadCdmZip(string workingDirectory)
        {
            Log.Information("Using {WorkingDirectory} as working directory.", workingDirectory);

            if (Directory.Exists(workingDirectory))
            {
                Log.Information("{WorkingDirectory} already exists, cleaning up..", workingDirectory);

                Directory.Delete(workingDirectory, true);
            }

            Directory.CreateDirectory(workingDirectory);

            var cdmLocationPath = Path.Join(workingDirectory, "master.zip");
            using (var client = new WebClient())
            {
                var downloadLocation = "https://github.com/Microsoft/CDM/archive/master.zip";
                Log.Information("Downloading CDM ZIP from {DownloadLocation} to {ZipLocation}", downloadLocation,
                    cdmLocationPath);
                client.DownloadFile(new Uri(downloadLocation), cdmLocationPath);
                Log.Information("Finished downloading");
            }

            return cdmLocationPath;
        }
    }
}