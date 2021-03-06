//
// NamedArgumentCompletionTests.cs
//
// Author:
//       Mike Krüger <mkrueger@xamarin.com>
//
// Copyright (c) 2013 Xamarin Inc. (http://xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using NUnit.Framework;

using MonoDevelop.CSharp.Parser;
using Mono.TextEditor;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using ICSharpCode.NRefactory;
using ICSharpCode.NRefactory.TypeSystem;
using MonoDevelop.Ide.TypeSystem;
using MonoDevelop.Ide.Gui.Content;
using MonoDevelop.CSharp.Formatting;
using UnitTests;
using MonoDevelop.Projects.Policies;
using MonoDevelop.CSharpBinding.Tests;
using MonoDevelop.Ide.Gui;
using MonoDevelop.CSharp.Completion;
using MonoDevelop.Ide.CodeCompletion;

namespace MonoDevelop.CSharpBinding
{
	[TestFixture]
	public class NamedArgumentCompletionTests : TestBase
	{
		class TestCompletionWidget : ICompletionWidget 
		{
			Document doc;

			public TestCompletionWidget (Document doc)
			{
				this.doc = doc;
			}

			public string CompletedWord {
				get;
				set;
			}
			#region ICompletionWidget implementation
			public event EventHandler CompletionContextChanged {
				add { /* TODO */ }
				remove { /* TODO */ }
			}

			public string GetText (int startOffset, int endOffset)
			{
				return doc.Editor.GetTextBetween (startOffset, endOffset);
			}

			public char GetChar (int offset)
			{
				return  doc.Editor.GetCharAt (offset);
			}

			public CodeCompletionContext CreateCodeCompletionContext (int triggerOffset)
			{
				var line = doc.Editor.GetLineByOffset (triggerOffset); 
				return new CodeCompletionContext {
					TriggerOffset = triggerOffset,
					TriggerLine = line.LineNumber,
					TriggerLineOffset = line.Offset,
					TriggerXCoord = 0,
					TriggerYCoord = 0,
					TriggerTextHeight = 0,
					TriggerWordLength = 0
				};
			}

			public CodeCompletionContext CurrentCodeCompletionContext {
				get {
					return CreateCodeCompletionContext (doc.Editor.Caret.Offset);
				}
			}

			public string GetCompletionText (CodeCompletionContext ctx)
			{
				return "";
			}

			public void SetCompletionText (CodeCompletionContext ctx, string partial_word, string complete_word)
			{
				this.CompletedWord = complete_word;
			}

			public void SetCompletionText (CodeCompletionContext ctx, string partial_word, string complete_word, int offset)
			{
				this.CompletedWord = complete_word;
			}

			public void Replace (int offset, int count, string text)
			{
			}

			public int CaretOffset {
				get {
					return doc.Editor.Caret.Offset;
				}
			}

			public int TextLength {
				get {
					return doc.Editor.Document.TextLength;
				}
			}

			public int SelectedLength {
				get {
					return 0;
				}
			}

			public Gtk.Style GtkStyle {
				get {
					return null;
				}
			}
			#endregion
		}


		static CSharpCompletionTextEditorExtension Setup (string input, out TestViewContent content)
		{
			TestWorkbenchWindow tww = new TestWorkbenchWindow ();
			content = new TestViewContent ();
			tww.ViewContent = content;
			content.ContentName = "a.cs";
			content.GetTextEditorData ().Document.MimeType = "text/x-csharp";

			Document doc = new Document (tww);

			var text = input;
			int endPos = text.IndexOf ('$');
			if (endPos >= 0)
				text = text.Substring (0, endPos) + text.Substring (endPos + 1);

			content.Text = text;
			content.CursorPosition = System.Math.Max (0, endPos);


			var compExt = new CSharpCompletionTextEditorExtension ();
			compExt.Initialize (doc);
			content.Contents.Add (compExt);

			doc.UpdateParseDocument ();
			return compExt;
		}

		string Test(string input, string type, string member, Gdk.Key key = Gdk.Key.Return)
		{
			TestViewContent content;
			var ext = Setup (input, out content);

			ListWindow.ClearHistory ();
			var listWindow = new CompletionListWindow ();
			var widget = new TestCompletionWidget (ext.Document);
			listWindow.CompletionWidget = widget;
			listWindow.CodeCompletionContext = widget.CurrentCodeCompletionContext;
			var t = ext.Document.Compilation.FindType (new FullTypeName (type)); 
			var foundMember = t.GetMembers (m => m.Name == member).First ();
			var data = new MemberCompletionData (ext, foundMember, OutputFlags.ClassBrowserEntries);
			data.DisplayFlags |= ICSharpCode.NRefactory.Completion.DisplayFlags.NamedArgument;
			KeyActions ka = KeyActions.Process;
			data.InsertCompletionText (listWindow, ref ka, key, (char)key, Gdk.ModifierType.None, true, false); 
			return widget.CompletedWord;
		}

		
		[Test]
		public void TestSimpleCase ()
		{
			string completion = Test (@"class MyClass
{
	int foo;
	void MyMethod ()
	{
		$
	}
}", "MyClass", "foo");
			Assert.AreEqual ("foo = ", completion); 
		}

		
		[Test]
		public void TestNoAutoCase ()
		{
			string completion = Test (@"class MyClass
{
	int foo;
	void MyMethod ()
	{
		$
	}
}", "MyClass", "foo", Gdk.Key.space);
			Assert.AreEqual ("foo", completion); 
		}


	}
}

