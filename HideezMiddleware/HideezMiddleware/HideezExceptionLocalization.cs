﻿using Hideez.SDK.Communication;
using Hideez.SDK.Communication.Log;
using HideezMiddleware.Resources;
using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Resources;

namespace HideezMiddleware
{
    public class HideezExceptionLocalization : Logger
    {
        public HideezExceptionLocalization(ILog log) 
            : base("Exception Localization", log)
        {
        }

        public static CultureInfo Culture
        {
            get { return ErrorCode.Culture; }
            set { ErrorCode.Culture = value; }
        }


        public bool VerifyResourcesForErrorCode(params string[] cultureNames)
        {
            bool isValid = true;
            foreach (string cultureName in cultureNames)
            {
                isValid |= VerifyResourcesForErrorCode(new CultureInfo(cultureName));
            }

            return isValid;
        }

        public bool VerifyResourcesForErrorCode(CultureInfo culture)
        {
            bool isValid = true;
            // English culture read from default resource
            ResourceSet resourceSet = ErrorCode.ResourceManager
                .GetResourceSet(culture, true, culture.EnglishName.StartsWith("en", StringComparison.InvariantCultureIgnoreCase));

            if (resourceSet == null)
            {
                isValid = false;
                WriteLine($"Has no resource for culture: {culture.EnglishName}", LogErrorSeverity.Warning);
            }

            var errorCodes = Enum.GetNames(typeof(HideezErrorCode));

            foreach (DictionaryEntry entry in resourceSet)
            {
                if (!errorCodes.Contains(entry.Key.ToString()))
                {
                    isValid = false;
                    WriteLine($"Resource contains key not suported in enum HideezErrorCode. " +
                        $"Key: {entry.Key.ToString()}, culture: {culture.EnglishName}", LogErrorSeverity.Warning);
                }
            }

            foreach (var errCode in errorCodes)
            {
                string str = resourceSet.GetString(errCode);

                if (str == null)
                {
                    isValid = false;
                    WriteLine($"HideezErrorCode is not set into resource. " +
                        $"HideezErrorCode: {errCode}, culture: {culture.EnglishName}", LogErrorSeverity.Warning);
                }
                else if (string.IsNullOrWhiteSpace(str))
                {
                    isValid = false;
                    WriteLine($"Value for HideezErrorCode cannot be empty or white space. " +
                        $"HideezErrorCode: {errCode}, culture: {culture.EnglishName}", LogErrorSeverity.Warning);
                }
            }

            return isValid;
        }

        public string GetErrorAsString(HideezErrorCode hideezErrorCode, CultureInfo culture = null)
        {
            return ErrorCode.ResourceManager.GetString(hideezErrorCode.ToString(), culture ?? ErrorCode.Culture);
        }

        public string GetErrorAsString(HideezException exception, CultureInfo culture = null)
        {
            string localizedStr = ErrorCode.ResourceManager.GetString(exception.ErrorCode.ToString(), culture ?? ErrorCode.Culture);

            if (exception.Parameters != null)
            {
                return string.Format(localizedStr, exception.Parameters);
            }
            else
            {
                return localizedStr;
            }
        }
    }
}
