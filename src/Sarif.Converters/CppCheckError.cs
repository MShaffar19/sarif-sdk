﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Xml;

namespace Microsoft.CodeAnalysis.Sarif.Converters
{
    /// <summary>An error reported by CppCheck.</summary>
    internal class CppCheckError
    {
        /// <summary>The rule ID of the error generated by CppCheck.</summary>
        public readonly string Id;
        /// <summary>The message of the error generated by CppCheck.</summary>
        public readonly string Message;
        /// <summary>The verbose message of the error generated by CppCheck.</summary>
        public readonly string VerboseMessage;
        /// <summary>The severity of the error generated by CppCheck.</summary>
        public readonly string Severity;
        /// <summary>The locations to which the error generated by CppCheck refer.</summary>
        public readonly ImmutableArray<CppCheckLocation> Locations;

        /// <summary>Initializes a new instance of the <see cref="CppCheckError"/> class.</summary>
        /// <param name="id">The rule ID of the error generated by CppCheck.</param>
        /// <param name="message">The message of the error generated by CppCheck.</param>
        /// <param name="verboseMessage">The verbose message of the error generated by CppCheck.</param>
        /// <param name="severity">The severity of the error generated by CppCheck.</param>
        /// <param name="locations">The locations to which the error generated by CppCheck refer.</param>
        public CppCheckError(string id, string message, string verboseMessage, string severity, ImmutableArray<CppCheckLocation> locations)
        {
            if (locations.IsDefaultOrEmpty)
            {
                throw new ArgumentException(ConverterResources.CppCheckMissingLocation);
            }

            this.Id = id;
            this.Message = message;
            this.VerboseMessage = verboseMessage;
            this.Severity = severity;
            this.Locations = locations;
        }

        /// <summary>
        /// Parses the element on which an <see cref="XmlReader"/> is positioned as a CppCheck error node.
        /// </summary>
        /// <param name="reader">The reader from which a CppCheck error shall be read.</param>
        /// <param name="strings">Strings used to parse the CppCheck log.</param>
        /// <returns>
        /// A <see cref="CppCheckError"/> parsed from the XML on which <paramref name="reader"/> is
        /// positioned.
        /// </returns>
        /// <exception cref="XmlException">The xml on which <paramref name="reader"/> is placed is
        /// in an incorrect format.</exception>
        public static CppCheckError Parse(XmlReader reader, CppCheckStrings strings)
        {
            if (!reader.IsStartElement(strings.Error))
            {
                throw reader.CreateException(ConverterResources.CppCheckElementNotError);
            }

            string id = null;
            string message = null;
            string verboseMessage = null;
            string severity = null;
            while (reader.MoveToNextAttribute())
            {
                string attributeName = reader.LocalName;
                if (StringReference.AreEqual(attributeName, strings.Id))
                {
                    id = reader.Value;
                }
                else if (StringReference.AreEqual(attributeName, strings.Msg))
                {
                    message = reader.Value;
                }
                else if (StringReference.AreEqual(attributeName, strings.Verbose))
                {
                    verboseMessage = reader.Value;
                }
                else if (StringReference.AreEqual(attributeName, strings.Severity))
                {
                    severity = reader.Value;
                }
            }

            reader.MoveToElement();
            ImmutableArray<CppCheckLocation> locations = ParseLocationsSubtree(reader, strings);

            reader.Read(); // Consumes the end element or self closing element and positions the reader on the next node

            try
            {
                return new CppCheckError(
                    id,
                    message,
                    verboseMessage,
                    severity,
                    locations
                    );
            }
            catch (ArgumentException ex)
            {
                throw reader.CreateException(ex.Message);
            }
        }

        /// <summary>Converts this instance to <see cref="Result"/>.</summary>
        /// <returns>This instance as an <see cref="Result"/>.</returns>
        public Result ToSarifIssue()
        {
            if (this.Locations.Length == 0)
            {
                throw new InvalidOperationException("At least one location must be present in a SARIF result.");
            }

            var result = new Result
            {
                RuleId = this.Id,
            };

            result.SetProperty("Severity", this.Severity);

            if (!string.IsNullOrEmpty(this.VerboseMessage))
            { 
                result.Message = new Message { Text = this.VerboseMessage };
            }
            else
            {
                result.Message = new Message { Text = this.Message };
            }

            PhysicalLocation lastLocationConverted;
            if (this.Locations.Length == 1)
            {
                lastLocationConverted = this.Locations[0].ToSarifPhysicalLocation();
            }
            else
            {
                var locations = new List<CodeFlowLocation>
                {
                    Capacity = this.Locations.Length
                };

                foreach (CppCheckLocation loc in this.Locations)
                {
                    locations.Add(new CodeFlowLocation
                    {
                        Location = new Location
                        {
                            PhysicalLocation = loc.ToSarifPhysicalLocation()
                        },
                        Importance = CodeFlowLocationImportance.Essential
                    });
                }

                result.CodeFlows = new List<CodeFlow>()
                {
                    SarifUtilities.CreateSingleThreadedCodeFlow(locations)
                };

                // In the N != 1 case, set the overall location's location to
                // the last entry in the execution flow.
                lastLocationConverted = locations[locations.Count - 1].Location.PhysicalLocation;
            }

            result.Locations = new List<Location>
            {
                new Location
                {
                    PhysicalLocation = lastLocationConverted
                }
            };

            return result;
        }

        private static ImmutableArray<CppCheckLocation> ParseLocationsSubtree(XmlReader reader, CppCheckStrings strings)
        {
            var locationBuilder = ImmutableArray.CreateBuilder<CppCheckLocation>();

            if (!reader.IsEmptyElement)
            {
                int startingDepth = reader.Depth;
                reader.Read();
                while (reader.Depth > startingDepth)
                {
                    Debug.Assert(reader.Depth == startingDepth + 1);
                    if (reader.NodeType == XmlNodeType.Whitespace)
                    {
                        reader.Read();
                    }
                    else
                    {
                        locationBuilder.Add(CppCheckLocation.Parse(reader, strings));
                    }
                }
            }

            ImmutableArray<CppCheckLocation> locations = locationBuilder.ToImmutable();
            return locations;
        }
    }
}
