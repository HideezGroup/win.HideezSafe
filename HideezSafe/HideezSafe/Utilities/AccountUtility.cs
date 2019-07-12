﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HideezSafe.Utilities
{
    static class AccountUtility
    {
        public static string[] Split(string data)
        {
            if (string.IsNullOrEmpty(data))
                return Array.Empty<string>();

            // string[] separators = { "\r\n", "\n", "\r" };
            string[] words = data.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < words.Length; i++)
                words[i] = words[i].Trim();

            return words;
        }
    }
}
