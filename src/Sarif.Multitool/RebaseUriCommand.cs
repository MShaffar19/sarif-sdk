﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis.Sarif.Driver;
using Microsoft.CodeAnalysis.Sarif.Processors;
using Newtonsoft.Json;

namespace Microsoft.CodeAnalysis.Sarif.Multitool
{
    internal class RebaseUriCommand : CommandBase
    {
        private readonly IFileSystem _fileSystem;

        public RebaseUriCommand(IFileSystem fileSystem = null)
        {
            _fileSystem = fileSystem ?? new FileSystem();
        }

        public int Run(RebaseUriOptions rebaseOptions)
        {
            try
            {
                if (!Uri.TryCreate(rebaseOptions.BasePath, UriKind.Absolute, out Uri baseUri))
                {
                    Console.Error.WriteLine($"The value '{rebaseOptions.BasePath}' of the --base-path-value option is not an absolute URI.");
                    return 1;
                }

                // In case someone accidentally passes C:\bld\src and meant C:\bld\src\--the base path should always be a folder, not something that points to a file.
                if (!string.IsNullOrEmpty(baseUri.GetFileName()))
                {
                    baseUri = new Uri(baseUri.ToString() + "/");
                }

                var rebaseUriFiles = GetRebaseUriFiles(rebaseOptions);

                bool outputFilesCanBeCreated = DriverUtilities.ReportWhetherOutputFilesCanBeCreated(rebaseUriFiles.Select(f => f.OutputFilePath), rebaseOptions.Force, _fileSystem);
                if (!outputFilesCanBeCreated) { return 1; }

                if (!rebaseOptions.Inline)
                {
                    _fileSystem.CreateDirectory(rebaseOptions.OutputFolderPath);
                }

                var formatting = rebaseOptions.PrettyPrint
                    ? Formatting.Indented
                    : Formatting.None;

                foreach (var rebaseUriFile in rebaseUriFiles)
                {
                    rebaseUriFile.Log = rebaseUriFile.Log.RebaseUri(rebaseOptions.BasePathToken, rebaseOptions.RebaseRelativeUris, baseUri);

                    WriteSarifFile(_fileSystem, rebaseUriFile.Log, rebaseUriFile.OutputFilePath, formatting);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return 1;
            }

            return 0;
        }
        
        private IEnumerable<RebaseUriFile> GetRebaseUriFiles(RebaseUriOptions rebaseUriOptions)
        {
            // Get files names first, as we may write more sarif logs to the same directory as we rebase them.
            HashSet<string> inputFilePaths = CreateTargetsSet(rebaseUriOptions.TargetFileSpecifiers, rebaseUriOptions.Recurse, _fileSystem);
            foreach(var inputFilePath in inputFilePaths)
            {
                yield return new RebaseUriFile
                {
                    InputFilePath = inputFilePath,
                    OutputFilePath = GetOutputFilePath(inputFilePath, rebaseUriOptions),
                    Log = ReadSarifFile<SarifLog>(_fileSystem, inputFilePath)
                };
            }
        }

        internal string GetOutputFilePath(string inputFilePath, RebaseUriOptions rebaseUriOptions)
        {
            if (rebaseUriOptions.Inline) { return inputFilePath; }

            return !string.IsNullOrEmpty(rebaseUriOptions.OutputFolderPath)
                ? Path.GetFullPath(rebaseUriOptions.OutputFolderPath) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(inputFilePath) + "-rebased.sarif"
                : Path.GetDirectoryName(inputFilePath) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(inputFilePath) + "-rebased.sarif";
        }

        private class RebaseUriFile
        {
            public string InputFilePath;
            public string OutputFilePath;

            public SarifLog Log;
        }
    }
}
