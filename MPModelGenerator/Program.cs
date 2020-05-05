using Platform.Clients.PowerService;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MPModelGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Count() == 0)
            {
                Console.WriteLine("MP Model Generator Utility");
                Console.WriteLine("This tool will produce poco models from all mapped meta-data in the MP REST api.  It will also produce a base class that all model will inherit.  The file mpmodels.cs will be created with all models.");
                Console.WriteLine("usage: mpmodelgenerator <api base url> <client> <secret>");
                Console.WriteLine("example: mpmodelgenerator https://demo.ministryplatform.net/ministryplatformapi myapiclient myapisecret");
                Console.WriteLine("Press <ENTER> key to exit...");
                var returnVal = Console.ReadLine();
                return;
            }

            // Check that Arg[0] is a Uri
            try
            {
                var uriTest = new Uri(args[0]);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception Occurred: {ex.Message}");
                Console.WriteLine("Argument 0 must be a valid Uri.");
                Console.WriteLine("Example: (https://demo.ministryplatform.net/ministryplatformapi)");
                var returnVal = Console.ReadLine();
                return;
            }



            // Initialize the api
            var _api = PowerServiceClientFactory.CreateAsync(new Uri(args[0]), args[1], args[2]).Result;

            Task.Run(async () => { await GenerateModels(_api); }).GetAwaiter().GetResult();
        }

        private static async Task GenerateModels(IPowerService _api)
        {
            string currentFolder = AppDomain.CurrentDomain.BaseDirectory;
            var tables = await _api.GetTablesAsync();
            
            using (StreamWriter sw = File.CreateText(currentFolder + "\\MPModels.cs"))
            {
                sw.WriteLine("using System;");
                sw.WriteLine("using System.Collections.Generic;");
                sw.WriteLine("using System.ComponentModel.DataAnnotations;");
                sw.WriteLine("using System.ComponentModel.DataAnnotations.Schema;");
                sw.WriteLine("using System.Linq;");
                sw.WriteLine("using Newtonsoft.Json;");
                sw.WriteLine("using System.Web.DynamicData;");
                sw.WriteLine("");
                sw.WriteLine("namespace MinistryPlatform.Models");
                sw.WriteLine("{");

                // Produce Base Class (Optional)
                //sw.WriteLine("\tpublic class mpBaseClass");
                //sw.WriteLine("\t{");
                //sw.WriteLine("\t}");

                foreach (var item in tables)
                {
                    // Ignore all Tables starting with _ character
                    if (item.Name.StartsWith("_"))
                        continue;

                    // Ignore SQL Views
                    if (item.Name.StartsWith("mp_vw"))
                        continue;

                    string modelName = item.Name.Replace("_", "");

                    modelName = fixModelName(modelName);

                    modelName += "Model";
                    //sw.WriteLine("[Table(\"" + item.Name + "\")]");
                    sw.WriteLine($"\tpublic class {modelName}");
                    sw.WriteLine("\t{");
                    foreach (var c in item.Columns)
                    {
                        //Ignore any field called tablename // Reserved Constant for Model
                        if (c.Name.ToLower() == "tablename")
                            continue;
                        
                        WriteColumn(c, sw);
                    }
                    sw.WriteLine("");
                    sw.WriteLine($"\t\tpublic const string TableName = \"{item.Name}\";");
                    sw.WriteLine("\t}");
                    sw.WriteLine("");
                    sw.WriteLine("");
                }
                sw.WriteLine("}");
            }

            return;
        }

        private static string fixModelName(string modelName)
        {
            if (modelName.EndsWith("Companies"))
            {
                return modelName.Replace("Companies", "Company");
            }

            if (modelName.EndsWith("Series"))
                return modelName;

            if (modelName.EndsWith("Addresses"))
            {
                return "Address";
            }

            //Testimonies
            if (modelName.EndsWith("ies"))
            {
                modelName = modelName.Remove(modelName.LastIndexOf("ies"));
                return modelName + "y";
            }

            //.TrimEnd('s');
            if (modelName.EndsWith("ses"))
            {
                modelName = modelName.Remove(modelName.LastIndexOf("es"));
                return modelName;
            }

            if (modelName.EndsWith("s"))
            {
                modelName = modelName.TrimEnd('s');
            }

            return modelName;
        }

        private static string GetDataType(string APIDataType)
        {
            switch (APIDataType)
            {
                case "Integer16":
                case "Integer32":
                case "Integer64":
                    return "int";

                case "Date":
                case "Time":
                case "DateTime":
                    return "DateTime";

                case "Real":
                case "Decimal":
                case "Money":
                    return "decimal";

                case "Boolean":
                    return "bool";

                default:
                    return "string";
            }
        }

        private static void WriteColumn(ColumnInfo c, StreamWriter sw)
        {
            //Do not include columns that begin in digits
            if (char.IsDigit(c.Name[0]))
                return;

            if (c.DataType.ToString() == "Separator")
                return;

            if (c.DataType.ToString() == "Password")
                return;

            if (c.IsRequired)
            {
                sw.WriteLine("\t\t[Required]");
            }

            if (c.IsPrimaryKey)
            {
                sw.WriteLine("\t\t[Key]");
            }

            if (GetDataType(c.DataType.ToString()) == "string")
            {
                if (c.Size > 0 && c.Size != 2147483647)
                {
                    sw.WriteLine("\t\t[MaxLength(" + c.Size.ToString() + ")]");
                }
            }

            if (c.Name.Contains("-") || c.Name.Contains("/"))
            {
                sw.WriteLine("\t\t[JsonProperty(PropertyName = \"" + c.Name + "\")]");
            }

            if (c.IsRequired || GetDataType(c.DataType.ToString()) == "string")
            {
                sw.WriteLine("\t\tpublic " + GetDataType(c.DataType.ToString()) + " " + c.Name.Replace('-', '_').Replace('/', '_') + " { get; set; }");
            }
            else
            {
                sw.WriteLine("\t\tpublic " + GetDataType(c.DataType.ToString()) + "? " + c.Name.Replace('-', '_') + " { get; set; }");
            }
        }
    }
}
