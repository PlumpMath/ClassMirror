using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ClassMirror {
    class Options {
        static private readonly char _optionSeparator = ':';
        static private bool _isWatching = false;
        static private Exception _error;
        static private Dictionary<string, FileSystemWatcher> _fsws = new Dictionary<string, FileSystemWatcher>();

        static public event Action<Options> ConfigurationChanged = o => { };
        static public event Action<Exception> ConfigurationError = e => { };

        public IList<Source> Sources = new List<Source>();
        public string DllName;
        public string BaseDir;
        public string Prefix = "extern \"C\"";

        private Options() { }

        static public void ThrowAnyConfigurationError() {
            if (_error != null) {
                throw _error;
            }
        }

        static public void StartWatching(string path) {
            string folder = Path.Combine(Directory.GetCurrentDirectory(), Path.GetDirectoryName(path));
            string extension = "*" + Path.GetExtension(path);
            string filename = Path.GetFileName(path);
            var fsw = new FileSystemWatcher(folder, extension) {
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime
            };
            fsw.Changed += (o, file) => {
                if (file.Name.ToLower() == filename) {
                    try {
                        ConfigurationChanged(Options.Load(file.FullPath));
                    } catch (Exception exception) {
                        _error = exception;
                        ConfigurationError(exception);
                    }
                }   
            };
        }

        static private string SplitOptionLine(string optionLine, int index) {
            var tokens = optionLine.Split(new [] { _optionSeparator }, 2);
            if (tokens.Length != 2) {
                throw new Exception(string.Format("Configuration line: {0} could not be parsed, format is 'option: value'"));
            }
            return tokens[index].Trim();
        }

        static private string GetKey(string optionLine) {
            return SplitOptionLine(optionLine, 0);            
        }

        static private string GetValue(string optionLine) {
            return SplitOptionLine(optionLine, 1);
        }

        private void SaveDllName(string name) {
            DllName = name.Trim();
        }

        private void SavePrefix(string name) {
            Prefix = name.Trim();
        }

        private void ParseSource(string options) {
            var tokens = options.Split(' ');
            if (tokens.Length != 4) {
                throw new Exception(string.Format(
                    "Configuration line: {0} could not be parsed, format is 'source: CppTypename header cexports csgen'",
                    options));
            }
            Sources.Add(new Source(tokens[0], tokens[1], Path.Combine(BaseDir, tokens[2]), Path.Combine(BaseDir, tokens[3])));
        }

        static public Options Load(string filename) {
            var options = new Options { 
                BaseDir = Path.Combine(Directory.GetCurrentDirectory(), Path.GetDirectoryName(filename))
            };
            var settings = new[] {
                new {
                    Key = "source",
                    Parse = new Action<string>(options.ParseSource)
                },
                new {
                    Key = "dllname",
                    Parse = new Action<string>(options.SaveDllName)
                },
                new {
                    Key = "prefix",
                    Parse = new Action<string>(options.SavePrefix)
                }
            };
            var config = File.ReadAllLines(filename).ToLookup(GetKey, GetValue);
            foreach (var setting in settings) {
                foreach (string value in config[setting.Key]) {
                    setting.Parse(value);
                }
            }
            if (string.IsNullOrEmpty(options.DllName)) {
                throw new Exception("No configuration entry found for dllname");
            }
            if (options.Sources.Count == 0) {
                throw new Exception("No sources found");
            }
            return options;
        }
    }
}
