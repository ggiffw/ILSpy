// Copyright (c) 2026 joyce
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

using ICSharpCode.Decompiler.CSharp.Syntax;

namespace ICSharpCode.Decompiler.CSharp.OutputVisitor;

/// <summary>
/// Projects metadata names to legal C# tokens without changing AST names or symbols.
/// </summary>
internal sealed class IdentifierOutputNames
{
	readonly Dictionary<string, string> names = new Dictionary<string, string>(StringComparer.Ordinal);
	readonly HashSet<string> reserved = new HashSet<string>(StringComparer.Ordinal);

	public IdentifierOutputNames(AstNode node)
	{
		while (node.Parent != null)
			node = node.Parent;
		var identifiers = node.DescendantsAndSelf.OfType<Identifier>().Select(i => i.Name).Distinct(StringComparer.Ordinal).ToArray();
		foreach (string name in identifiers)
		{
			if (IsValid(name))
				reserved.Add(name);
		}
		foreach (string name in identifiers.OrderBy(n => n, StringComparer.Ordinal))
			GetName(name);
	}

	public string GetName(string name)
	{
		if (IsValid(name))
			return name;
		if (names.TryGetValue(name, out string result))
			return result;
		var builder = new StringBuilder();
		if (IsPart(name[0]) && !IsStart(name[0]))
			builder.Append('_');
		foreach (char ch in name)
		{
			if (IsPart(ch))
				builder.Append(ch);
			else
				builder.Append('_').Append(((int)ch).ToString("X4", CultureInfo.InvariantCulture));
		}
		string basis = builder.ToString();
		result = basis;
		for (int suffix = 1; !reserved.Add(result); suffix++)
			result = basis + "_" + suffix.ToString(CultureInfo.InvariantCulture);
		names.Add(name, result);
		return result;
	}

	static bool IsValid(string name)
	{
		if (name.Length == 0)
			return true;
		if (!IsStart(name[0]))
			return false;
		for (int i = 1; i < name.Length; i++)
		{
			if (!IsPart(name[i]))
				return false;
		}
		return true;
	}

	static bool IsStart(char ch)
	{
		if (ch == '_')
			return true;
		switch (char.GetUnicodeCategory(ch))
		{
			case UnicodeCategory.UppercaseLetter:
			case UnicodeCategory.LowercaseLetter:
			case UnicodeCategory.TitlecaseLetter:
			case UnicodeCategory.ModifierLetter:
			case UnicodeCategory.OtherLetter:
			case UnicodeCategory.LetterNumber:
				return true;
			default:
				return false;
		}
	}

	static bool IsPart(char ch)
	{
		if (IsStart(ch))
			return true;
		switch (char.GetUnicodeCategory(ch))
		{
			case UnicodeCategory.NonSpacingMark:
			case UnicodeCategory.SpacingCombiningMark:
			case UnicodeCategory.DecimalDigitNumber:
			case UnicodeCategory.ConnectorPunctuation:
				return true;
			default:
				// Format characters are ignored by C# identifier identity. Encode them
				// so that distinct metadata names cannot bind to the same C# symbol.
				return false;
		}
	}
}
