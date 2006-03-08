//
// CombineNodeBuilder.cs
//
// Author:
//   Lluis Sanchez Gual
//
// Copyright (C) 2005 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections;

using MonoDevelop.Projects;
using MonoDevelop.Core;
using MonoDevelop.Ide.Commands;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Core.Gui;
using MonoDevelop.Components.Commands;
using MonoDevelop.Ide.Gui.Search;

namespace MonoDevelop.Ide.Gui.Pads.ProjectPad
{
	public class CombineNodeBuilder: TypeNodeBuilder
	{
		CombineEntryEventHandler combineEntryAdded;
		CombineEntryEventHandler combineEntryRemoved;
		CombineEntryRenamedEventHandler combineNameChanged;
		
		public CombineNodeBuilder ()
		{
			combineEntryAdded = (CombineEntryEventHandler) Services.DispatchService.GuiDispatch (new CombineEntryEventHandler (OnEntryAdded));
			combineEntryRemoved = (CombineEntryEventHandler) Services.DispatchService.GuiDispatch (new CombineEntryEventHandler (OnEntryRemoved));
			combineNameChanged = (CombineEntryRenamedEventHandler) Services.DispatchService.GuiDispatch (new CombineEntryRenamedEventHandler (OnCombineRenamed));
		}

		public override Type NodeDataType {
			get { return typeof(Combine); }
		}
		
		public override Type CommandHandlerType {
			get { return typeof(CombineNodeCommandHandler); }
		}
		
		public override string GetNodeName (ITreeNavigator thisNode, object dataObject)
		{
			return ((Combine)dataObject).Name;
		}
		
		public override void GetNodeAttributes (ITreeNavigator treeNavigator, object dataObject, ref NodeAttributes attributes)
		{
			attributes |= NodeAttributes.AllowRename;
		}
		
		public override string ContextMenuAddinPath {
			get { return "/SharpDevelop/Views/ProjectBrowser/ContextMenu/CombineBrowserNode"; }
		}
		
		public override void BuildNode (ITreeBuilder treeBuilder, object dataObject, ref string label, ref Gdk.Pixbuf icon, ref Gdk.Pixbuf closedIcon)
		{
			Combine combine = dataObject as Combine;
			
			switch (combine.Entries.Count) {
				case 0:
					label = String.Format (GettextCatalog.GetString ("Solution {0}"), combine.Name);
					break;
				case 1:
					label = String.Format (GettextCatalog.GetString ("Solution {0} (1 entry)"), combine.Name);
					break;
				default:
					label = String.Format (GettextCatalog.GetString ("Solution {0} ({1} entries)"), combine.Name, combine.Entries.Count);
					break;
			}

			icon = Context.GetIcon (Stock.CombineIcon);
		}

		public override void BuildChildNodes (ITreeBuilder ctx, object dataObject)
		{
			Combine combine = (Combine) dataObject;
			foreach (CombineEntry entry in combine.Entries)
				ctx.AddChild (entry);
		}

		public override bool HasChildNodes (ITreeBuilder builder, object dataObject)
		{
			return ((Combine) dataObject).Entries.Count > 0;
		}
		
		public override object GetParentObject (object dataObject)
		{
			return ((CombineEntry) dataObject).ParentCombine;
		}
		
		public override int CompareObjects (ITreeNavigator thisNode, ITreeNavigator otherNode)
		{
			if (otherNode.DataItem is Combine)
				return DefaultSort;
			else
				return -1;
		}

		public override void OnNodeAdded (object dataObject)
		{
			Combine combine = (Combine) dataObject;
			combine.EntryAdded += combineEntryAdded;
			combine.EntryRemoved += combineEntryRemoved;
			combine.NameChanged += combineNameChanged;
		}
		
		public override void OnNodeRemoved (object dataObject)
		{
			Combine combine = (Combine) dataObject;
			combine.EntryAdded -= combineEntryAdded;
			combine.EntryRemoved -= combineEntryRemoved;
			combine.NameChanged -= combineNameChanged;
		}
		
		void OnEntryAdded (object sender, CombineEntryEventArgs e)
		{
			ITreeBuilder tb = Context.GetTreeBuilder (sender);
			if (tb != null) {
				tb.Update ();	// Update the entry count
				tb.AddChild (e.CombineEntry, true);
				tb.Expanded = true;
			}
		}

		void OnEntryRemoved (object sender, CombineEntryEventArgs e)
		{
			ITreeBuilder tb = Context.GetTreeBuilder (e.CombineEntry);
			if (tb != null) {
				tb.Remove ();
				if (tb.MoveToParent (typeof(Combine)))
					tb.Update ();	// Update the entry count
			}
		}
		
		void OnCombineRenamed (object sender, CombineEntryRenamedEventArgs e)
		{
			ITreeBuilder tb = Context.GetTreeBuilder (e.CombineEntry);
			if (tb != null) tb.Update ();
		}
	}
	
	public class CombineNodeCommandHandler: NodeCommandHandler
	{
		public override void RenameItem (string newName)
		{
			if (newName.IndexOfAny (new char [] { '\'', '(', ')', '"', '{', '}', '|' } ) != -1) {
				Services.MessageService.ShowError (String.Format (GettextCatalog.GetString ("Solution name may not contain any of the following characters: {0}"), "', (, ), \", {, }, |"));
				return;
			}
			
			Combine combine = (Combine) CurrentNode.DataItem;
			combine.Name = newName;
			IdeApp.ProjectOperations.SaveCombine();
		}
		
		public override DragOperation CanDragNode ()
		{
			return DragOperation.Move;
		}
		
		public override bool CanDropNode (object dataObject, DragOperation operation)
		{
			return dataObject is CombineEntry;
		}
		
		public override void OnNodeDrop (object dataObject, DragOperation operation)
		{
		}
		
		[CommandHandler (EditCommands.Delete)]
		public void RemoveItem ()
		{
			Combine combine = CurrentNode.DataItem as Combine;
			Combine parent = CurrentNode.GetParentDataItem (typeof(Combine), false) as Combine;
			if (parent == null) return;
			
			bool yes = Services.MessageService.AskQuestion (String.Format (GettextCatalog.GetString ("Do you really want to remove solution {0} from solution {1}?"), combine.Name, parent.Name));
			if (yes) {
				parent.Entries.Remove (combine);
				IdeApp.ProjectOperations.SaveCombine();
			}
		}
		
		[CommandHandler (ProjectCommands.AddNewProject)]
		public void AddNewProjectToCombine()
		{
			Combine combine = (Combine) CurrentNode.DataItem;
			CombineEntry ce = IdeApp.ProjectOperations.CreateProject (combine);
			if (ce == null) return;
			Tree.AddNodeInsertCallback (ce, new TreeNodeCallback (OnEntryInserted));
			CurrentNode.Expanded = true;
		}
		
		[CommandHandler (ProjectCommands.AddProject)]
		public void AddProjectToCombine()
		{
			Combine combine = (Combine) CurrentNode.DataItem;
			CombineEntry ce = IdeApp.ProjectOperations.AddCombineEntry (combine);
			if (ce == null) return;
			Tree.AddNodeInsertCallback (ce, new TreeNodeCallback (OnEntryInserted));
			CurrentNode.Expanded = true;
		}
		
		[CommandHandler (ProjectCommands.AddNewCombine)]
		public void AddNewCombineToCombine()
		{
			Combine combine = (Combine) CurrentNode.DataItem;
			CombineEntry ce = IdeApp.ProjectOperations.CreateCombine (combine);
			if (ce == null) return;
			Tree.AddNodeInsertCallback (ce, new TreeNodeCallback (OnEntryInserted));
			CurrentNode.Expanded = true;
		}
		
		[CommandHandler (ProjectCommands.AddCombine)]
		public void AddCombineToCombine()
		{
			Combine combine = (Combine) CurrentNode.DataItem;
			CombineEntry ce = IdeApp.ProjectOperations.AddCombineEntry (combine);
			if (ce == null) return;
			Tree.AddNodeInsertCallback (ce, new TreeNodeCallback (OnEntryInserted));
			CurrentNode.Expanded = true;
		}
		
		void OnEntryInserted (ITreeNavigator nav)
		{
			nav.Selected = true;
			nav.Expanded = true;
		}
		
		[CommandHandler (ProjectCommands.Options)]
		public void OnCombineOptions ()
		{
			IdeApp.ProjectOperations.ShowOptions ((Combine) CurrentNode.DataItem);
		}
		
		[CommandHandler (SearchCommands.FindInFiles)]
		public void OnFindInFiles ()
		{
			Combine combine = (Combine) CurrentNode.DataItem;
			SearchReplaceInFilesManager.SearchOptions.SearchDirectory = combine.BaseDirectory;
			SearchReplaceInFilesManager.ShowFindDialog ();
		}
	}
}
