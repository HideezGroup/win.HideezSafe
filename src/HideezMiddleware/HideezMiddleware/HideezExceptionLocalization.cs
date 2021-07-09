using Hideez.SDK.Communication;
using Hideez.SDK.Communication.Log;
using HideezMiddleware.Resources;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;

namespace HideezMiddleware
{
    public class HideezExceptionLocalization : Logger
    {
        static List<ResourceManager> _resourceManagers = new List<ResourceManager>();

        public HideezExceptionLocalization(ILog log) 
            : base(string.Empty, log)
        {
        }

        public static CultureInfo Culture
        {
            get { return ErrorCode.Culture; }
            set { ErrorCode.Culture = value; }
        }


        public bool VerifyResourcesForErrorCode(params string[] cultureNames)
        {
            if (!cultureNames.Any())
                throw new ArgumentException($"{nameof(cultureNames)} cannot be empty.");

            bool isValid = true;
            foreach (string cultureName in cultureNames)
            {
                isValid |= VerifyResourcesForErrorCode(new CultureInfo(cultureName));
            }

            return isValid;
        }

        public bool VerifyResourcesForErrorCode(CultureInfo culture)
        {
            if (culture == null)
                throw new ArgumentNullException(nameof(culture));

            bool isValid = true;

            // If specified culture is English, read from the default resource due to file name difference for default culture
            ResourceSet resourceSet = ErrorCode.ResourceManager
                .GetResourceSet(culture, true, culture.EnglishName.StartsWith("en", StringComparison.InvariantCultureIgnoreCase));

            if (resourceSet == null)
            {
                isValid = false;
                WriteLine($"Localization for {culture.EnglishName} culture is not available", LogErrorSeverity.Warning);
            }
            else
            {
                var errorCodes = Enum.GetNames(typeof(HideezErrorCode));

                foreach (DictionaryEntry entry in resourceSet)
                {
                    if (!errorCodes.Contains(entry.Key.ToString()))
                    {
                        isValid = false;
                        WriteLine($"No error code found for: " +
                            $"{entry.Key.ToString()}, culture: {culture.EnglishName}", LogErrorSeverity.Warning);
                    }
                }

                foreach (var errCode in errorCodes)
                {
                    string str = resourceSet.GetString(errCode);

                    if (str == null)
                    {
                        isValid = false;
                        WriteLine($"No localization for error code: " +
                            $"{errCode}, culture: {culture.EnglishName}", LogErrorSeverity.Warning);
                    }
                    else if (string.IsNullOrWhiteSpace(str))
                    {
                        isValid = false;
                        WriteLine($"Localization is empty for: " +
                            $"{errCode}, culture: {culture.EnglishName}", LogErrorSeverity.Warning);
                    }
                }
            }

            return isValid;
        }

        public static string GetErrorAsString(HideezErrorCode hideezErrorCode, CultureInfo culture = null)
        {
            for (int i = _resourceManagers.Count - 1; i >= 0; i--)
            {
                string result = _resourceManagers[i].GetString(hideezErrorCode.ToString(), culture ?? Culture);

                if (result != null)
                    return result;
            }

            return null;
        }

        public static string GetErrorAsString(Exception exception, CultureInfo culture = null)
        {
            if (exception is HideezException hideezException)
            {
                var parameters = hideezException.Parameters;
                var code = hideezException.ErrorCode;

                if (code == HideezErrorCode.ThirdPartyException)
                    return exception.Message;

                string localizedStr = GetErrorAsString(code, culture);

                if (localizedStr != null)
                {
                    if (parameters != null && parameters.Length > 0)
                        localizedStr = string.Format(localizedStr, parameters);
                }
                else
                {
                    localizedStr = code.ToString();
                }

                if (exception is HesException)
                    localizedStr = $"Server: {localizedStr}";

                return localizedStr;
            }
            else
            {
                return exception.Message;
            }
        }

        public static void SetResourceManagers(List<ResourceManager> resourceManagers)
        {
            _resourceManagers = resourceManagers;
        }
    }
}
