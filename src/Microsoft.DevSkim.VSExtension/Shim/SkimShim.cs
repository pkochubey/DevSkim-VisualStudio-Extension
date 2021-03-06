﻿// Copyright (C) Microsoft. All rights reserved.
// Licensed under the MIT License. See LICENSE.txt in the project root for license information.

using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Microsoft.DevSkim.VSExtension
{
    /// <summary>
    /// Shim around DevSkim. Parses code applies rules
    /// </summary>
    public class SkimShim
    {
        public SkimShim()
        {
            processor = new RuleProcessor()
            {
                EnableSuppressions = true
            };

            ruleset = new Ruleset();

            LoadRules();
        }

        #region Public Static Methods

        /// <summary>
        /// Reapllys settings
        /// </summary>
        public static void ApplySettings()
        {
            _instance.LoadRules();            
        }

        /// <summary>
        /// Indicates if there are more than one issue on the given line
        /// </summary>
        /// <param name="text">line of code</param>
        /// <param name="contenttype">VS Content Type</param>
        /// <returns>True if more than one issue exists</returns>
        public static bool HasMultipleProblems(string text, string contenttype)
        {                        
            var list = Analyze(text, contenttype, string.Empty)
                      .GroupBy(x => x.Issue.Rule.Id)
                      .Select(x => x.First())
                      .ToList();

            return list.Count() > 1;            
        }

        /// <summary>
        /// Analyze text for issues
        /// </summary>
        /// <param name="text">line of code</param>
        /// <param name="contenttype">VS Content Type</param>
        /// <returns>List of actionable and non-actionable issues</returns>
        public static Problem[] Analyze(string text, string contentType, string fileName)
        {            
            List<Problem> results = new List<Problem>();
            Issue[] issues = _instance.processor.Analyze(text, _instance.GetLanguageList(contentType, fileName));

            // Add matches for errors
            foreach (Issue issue in issues)
                results.Add(new Problem() { Actionable = true, Issue = issue});

            // Get list of IDs on the ignore list
            SuppressionEx supp = new SuppressionEx(text, contentType);
            string[] suppissues = supp.GetIssues();
            if (suppissues != null && suppissues.Count() > 0)
            {
                int index = supp.IssuesIndex;
                foreach (string id in suppissues)
                {
                    Issue issue = new Issue()
                    {
                        Index = index,
                        Length = id.Length,
                        Rule = _instance.ruleset.FirstOrDefault(x => x.Id == id)
                    };

                    if (issue.Rule != null)
                        results.Add(new Problem() { Actionable = false, Issue = issue });

                    index += id.Length + 1;
                }
            }

            return results.ToArray();
        }    

        #endregion

        #region Private

        /// <summary>
        /// Get list of applicable lenguages based on file name and VS content type
        /// </summary>
        /// <param name="contentType">Visual Studio content type</param>
        /// <param name="fileName">Filename</param>
        /// <returns></returns>
        private string[] GetLanguageList(string contentType, string fileName)
        {
            string flang = Language.FromFileName(fileName);
            List<string> langs = new List<string>(ContentType.GetLanguages(contentType));                

            if (!langs.Contains(flang))
            {
                langs.Add(flang);
            }

            return langs.ToArray();
        }

        /// <summary>
        /// Reloads rules based on settings
        /// </summary>
        private void LoadRules()
        {
            Settings set = Settings.GetSettings();

            string dir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            dir = Path.Combine(Path.Combine(dir, "Content"), "rules");
            if (set.UseDefaultRules)
                ruleset.AddDirectory(dir, null);

            if (set.UseCustomRules)
                ruleset.AddDirectory(set.CustomRulesPath, "custom");

            processor.Rules = ruleset;

            processor.SeverityLevel = Severity.Critical;

            if (set.EnableImportantRules) processor.SeverityLevel |= Severity.Important;
            if (set.EnableModerateRules) processor.SeverityLevel |= Severity.Moderate;
            if (set.EnableLowRules) processor.SeverityLevel |= Severity.Low;

            if (set.EnableInformationalRules) processor.SeverityLevel |= Severity.Informational;
            if (set.EnableDefenseInDepthRules) processor.SeverityLevel |= Severity.DefenseInDepth;
            if (set.EnableManualReviewRules) processor.SeverityLevel |= Severity.ManualReview;
        }

        private RuleProcessor processor = new RuleProcessor();
        private Ruleset ruleset;

        private static SkimShim _instance = new SkimShim();

        #endregion
    }
}
