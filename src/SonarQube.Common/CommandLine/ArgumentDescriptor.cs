/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
 * mailto:info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SonarQube.Common
{
    /// <summary>
    /// Data class that describes a single valid command line argument - id, prefixes, multiplicity etc
    /// </summary>
    [DebuggerDisplay("{Prefixes[0]}")]
    public class ArgumentDescriptor
    {
        // https://msdn.microsoft.com/en-us/library/ms973919.aspx
        // "[d]ata that is designed to be culture-agnostic and linguistically irrelevant should...
        //  use either StringComparison.Ordinal or StringComparison.OrdinalIgnoreCase..."
        public static readonly StringComparer IdComparer = StringComparer.Ordinal;

        public static readonly StringComparison IdComparison = StringComparison.Ordinal;

        private ArgumentDescriptor(string[] prefixes, bool required, string description, bool allowMultiple)
            : this(prefixes, required, description, allowMultiple, false /* not a verb */)
        {
        }

        private ArgumentDescriptor(string[] prefixes, bool required, string description, bool allowMultiple, bool isVerb)
        {
            if (prefixes == null || prefixes.Length == 0)
            {
                throw new ArgumentNullException("prefixes");
            }
            if (string.IsNullOrWhiteSpace(description))
            {
                throw new ArgumentNullException("description");
            }

            Prefixes = prefixes;
            Required = required;
            Description = description;
            AllowMultiple = allowMultiple;
            IsVerb = isVerb;
        }

        /// <summary>
        /// Any prefixes supported for the argument. This should include all of the characters that
        /// are not to be treated as part of the value e.g. /key=
        /// </summary>
        public string[] Prefixes { get; }

        /// <summary>
        /// Whether the argument is mandatory or not
        /// </summary>
        public bool Required { get; }

        /// <summary>
        /// A short description of the argument that will be displayed to the user
        /// e.g. /key= [SonarQube project key]
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// True if the argument can be specified multiple times,
        /// false if it can be specified at most once
        /// </summary>
        public bool AllowMultiple { get; }

        /// <summary>
        /// False if the argument has a value that follows the prefix,
        /// true if the argument is just single word (e.g. "begin")
        /// </summary>
        public bool IsVerb { get; }

        public bool IsMatch(ArgumentInstance argument) =>
            Equals(argument.Descriptor);

        public static ArgumentDescriptor Create(string[] prefixes, string description, bool required = false, bool allowMultiple = false) =>
            new ArgumentDescriptor(prefixes, required, description, allowMultiple);

        public static ArgumentDescriptor CreateVerb(string prefix, string description) =>
            new ArgumentDescriptor(new[] { prefix }, required: false, description: description, allowMultiple: false, isVerb: true);

        public bool Exists(IEnumerable<ArgumentInstance> arguments) =>
            arguments.Any(IsMatch);

        public bool TryGetArgumentValue(IEnumerable<ArgumentInstance> arguments, out string value)
        {
            value = arguments.FirstOrDefault(IsMatch)?.Value;
            return value != null;
        }
    }
}
