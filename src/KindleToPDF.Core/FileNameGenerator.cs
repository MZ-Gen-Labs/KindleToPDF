using System;
using System.IO;

namespace KindleToPDF.Core
{
    public static class FileNameGenerator
    {
        public static string GetOutputFilePath(string rawPath, AppSettings settings)
        {
            if (settings.Mode == FileNameMode.Overwrite)
            {
                return rawPath;
            }
            
            string dir = Path.GetDirectoryName(rawPath) ?? "";
            string fileName = Path.GetFileNameWithoutExtension(rawPath);
            string ext = Path.GetExtension(rawPath);
            string newPath = rawPath;
            
            switch (settings.SeqType)
            {
                case SequentialType.Number:
                    int currentNum = settings.StartNumber;
                    while (true)
                    {
                        string suffix = currentNum.ToString("D" + settings.NumberDigits);
                        newPath = Path.Combine(dir, $"{fileName}_{suffix}{ext}");
                        if (!File.Exists(newPath)) break;
                        currentNum++;
                    }
                    break;
                    
                case SequentialType.Alphabet:
                    string currentChar = settings.StartChar;
                    while (true)
                    {
                        newPath = Path.Combine(dir, $"{fileName}_{currentChar}{ext}");
                        if (!File.Exists(newPath)) break;
                        currentChar = IncrementAlphabet(currentChar);
                    }
                    break;
                    
                case SequentialType.DateTime:
                    string dateStr = DateTime.Now.ToString(settings.DateTimeFormat);
                    newPath = Path.Combine(dir, $"{fileName}_{dateStr}{ext}");
                    break;
            }
            return newPath;
        }

        private static string IncrementAlphabet(string s)
        {
            if (string.IsNullOrEmpty(s)) return "a";
            char last = s[s.Length - 1];
            if (last == 'z') return s + "a";
            if (last == 'Z') return s + "A";
            return s.Substring(0, s.Length - 1) + (char)(last + 1);
        }
    }
}
