﻿using Aga.Controls.Tree;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TabularEditor.PropertyGridUI;
using TabularEditor.TOMWrapper;
using TabularEditor.TOMWrapper.Utils;
using TabularEditor.UI.Actions;
using TabularEditor.UI.Tree;
using TabularEditor.UIServices;

namespace TabularEditor.UI
{
    public partial class UIController
    {
        TabularNodeTextBox TreeView_NameCol;

        public void Goto(ITabularNamedObject obj)
        {
            if (!Tree.VisibleInTree(obj))
            {
                Tree.BeginUpdate();
                Tree.Options = LogicalTreeOptions.Default | LogicalTreeOptions.ShowHidden;
                Tree.Filter = "";
                UI.FormMain.UpdateTreeUIButtons();
                Tree.EndUpdate();
            }

            var node = UI.TreeView.FindNodeByTag(obj);
            if (node != null)
            {
                UI.TreeView.EnsureVisible(node);
                UI.TreeView.SelectedNode = node;
                UI.FormMain.Activate();
                UI.TreeView.Focus();
            }
        }

        private void Tree_Init()
        {
            // Set up custom node controls:
            UI.TreeView.NodeControls.Insert(0, new TreeViewAdvExtension.NodeArrow { ParentColumn = UI.TreeView.Columns[0] });
            UI.TreeView.NodeControls.Insert(1, new TabularIcon { Images = UI.TreeImages.Images, ParentColumn = UI.TreeView.Columns[0] });
            TreeView_NameCol = new TabularNodeTextBox(this) { DataPropertyName = "LocalName", ParentColumn = UI.TreeView.Columns[0], Trimming = StringTrimming.EllipsisCharacter, EditEnabled = true };
            UI.TreeView.NodeControls.Insert(2, TreeView_NameCol);

            TreeView_NameCol.ChangesApplied += TvName_ChangesApplied;

            // Hook up events:
            UI.TreeView.SelectionChanged += TreeView_SelectionChanged;
            UI.TreeView.DragDrop += TreeView_DragDrop;
            UI.TreeView.ItemDrag += TreeView_ItemDrag;
            UI.TreeView.DragOver += TreeView_DragOver;
            UI.TreeView.DragEnter += TreeView_DragEnter;
            UI.TreeView.Leave += TreeView_Leave;
            UI.TreeView.NodeMouseDoubleClick += TreeView_NodeMouseDoubleClick;
            UI.TreeView.KeyDown += TreeView_KeyDown;
            UI.TreeView.BeforeMultiSelect += TreeView_BeforeMultiSelect;
            TreeView_NameCol.EditorShowing += TvName_EditorShowing;

            UI.TreeView.DragLeave += TreeView_DragLeave;

            // Set up context menu:
            var menu = new ContextMenuStrip();
            menu.Opening += ContextMenu_Opening;
            UI.TreeView.ContextMenuStrip = menu;
            UI.ToolsMenu.DropDown.Opening += ToolsMenu_Opening;
            UI.ModelMenu.DropDown.Opening += ModelMenu_Opening;
            UI.DynamicMenu.DropDown.Opening += DynamicMenu_Opening;
        }

        private void TvName_EditorShowing(object sender, CancelEventArgs e)
        {
            if(UI.TreeView.SelectedNode != null && UI.TreeView.SelectedNode.Tag is IDynamicPropertyObject)
            {
                e.Cancel = !(UI.TreeView.SelectedNode.Tag as IDynamicPropertyObject).Editable("Name");
            }
        }

        private void TreeView_BeforeMultiSelect(object sender, TreeViewAdvCancelEventArgs e)
        {
            if (e.Node.Tag is LogicalGroup) e.Cancel = true;
        }

        TreeNodeAdv[] draggedNodes = null;

        private void TreeView_DragLeave(object sender, EventArgs e)
        {
            if (Handler == null) return;

            if (draggedNodes != null && Tree_CurrentDragObject != null && Tree_CurrentDragObject.GetDataPresent(typeof(TreeNodeAdv[])))
            {
                draggedNodes = Tree_CurrentDragObject.GetData(typeof(TreeNodeAdv[])) as TreeNodeAdv[];
                Tree_CurrentDragObject.SetData(typeof(TreeNodeAdv[]), null);
            }
        }

        private void TreeView_DragEnter(object sender, DragEventArgs e)
        {
            if (Handler == null) return;

            if (draggedNodes != null)
            {
                e.Data.SetData("Aga.Controls.Tree.TreeNodeAdv[]", draggedNodes);
            } else if (e.Data.GetDataPresent("Aga.Controls.Tree.TreeNodeAdv[]"))
            {
                // Disabled below with build v. 2.6 as drag/drop between instances is buggy. Use CTRL+C / CTRL+V instead.

                // If the draggedNodes array is null, but the data object is supposed to contain tree nodes,
                // it means that nodes are being dragged from another instance of Tabular Editor. In this case,
                // we cannot deserialize the nodes on the data object, but will try to reconstruct them from
                // the serialized string value:
                //if (e.Data.GetDataPresent("Text"))
                //{
                //    var objects = Serializer.DeserializeObjects(e.Data.GetData("Text") as string);
                //    draggedNodes = objects.Select(obj => new TreeNodeAdv(obj)).ToArray();
                //}
            }
        }

        private void TreeView_KeyDown(object sender, KeyEventArgs e)
        {
            if (Handler == null) return;

            if (e.KeyCode == Keys.Enter)
            {
                var node = UI.TreeView.SelectedNode;
                if (node == null || UI.TreeView.CurrentEditor != null) return;
                if (node.Tag is IDAXExpressionObject) ExpressionEditor_Edit(node.Tag as IDAXExpressionObject);
                else if (!node.IsLeaf) if (node.IsExpanded) node.Collapse(); else node.Expand();
            }
            if(e.KeyCode == Keys.F && e.Modifiers.HasFlag(Keys.Control))
            {
                UI.FormMain.FocusFilter();
            }
            if(e.KeyCode == Keys.Delete)
            {
                Actions.Delete.DoExecute();
            }
        }

        /// <summary>
        /// This method toggles Explorer Tree options in a way suitable to show the given item.
        /// For example, of a filter is currently specified, but the given item does not fulfill
        /// the filter criteria, Filtering will be toggled off in the UI.
        /// </summary>
        /// <param name="item"></param>
        public TreeNodeAdv EnsureNodeVisible(ITabularNamedObject item)
        {
            var node = UI.TreeView.FindNodeByTag(item);
            if (node != null) return node;

            LogicalTreeOptions optionsToApply = 0;

            if((item as IHideableObject)?.IsHidden ?? false && !Tree.Options.HasFlag(LogicalTreeOptions.ShowHidden))
            {
                // If the object is hidden, make sure the tree is set up to show hidden objects:
                optionsToApply |= LogicalTreeOptions.ShowHidden;
                UI.FormMain.actToggleHidden.Checked = true;
            }

            switch(item.ObjectType)
            {
                case ObjectType.Table:
                    // Tables will always be visible
                case ObjectType.Column:
                case ObjectType.Measure:
                case ObjectType.Hierarchy:
                case ObjectType.Level:
                case ObjectType.KPI:
                default:
                    break;
            }

            return null;
        }

        public void EditName(ITabularNamedObject item)
        {
            var node = EnsureNodeVisible(item);

            if (node != null)
            {
                UI.TreeView.SelectedNode = node;
                TreeView_NameCol.BeginEdit();
            }
        }

        public void ExpandItem(ITabularNamedObject item)
        {
            var node = UI.TreeView.FindNodeByTag(item);
            if (node != null) node.Expand();
        }

        private List<Aga.Controls.Tree.NodeControls.NodeControl> orgControls;
        public void SetInfoColumns(bool showInfoColumns)
        {
            if (UI.TreeView.UseColumns)
            {
                orgControls = UI.TreeView.NodeControls.ToList();
            }
            UI.TreeView.UseColumns = showInfoColumns;
            UI.TreeView.FullRowSelect = UI.TreeView.UseColumns;
            if (!UI.TreeView.UseColumns)
            {
                orgControls = UI.TreeView.NodeControls.ToList();
                UI.TreeView.NodeControls.Clear();
                UI.TreeView.NodeControls.Add(orgControls[0]);
                UI.TreeView.NodeControls.Add(orgControls[1]);
                UI.TreeView.NodeControls.Add(orgControls[2]);
            }
            else
            {
                for (var i = 3; i < orgControls.Count; i++)
                {
                    UI.TreeView.NodeControls.Add(orgControls[i]);
                }
            }
        }

        #region TreeView events
        private void TreeView_SelectionChanged(object sender, EventArgs e)
        {
            SelectionInvalid = true;
            PropertyGrid_UpdateFromSelection();
            ExpressionEditor_Preview();
            ScriptEditor_HideErrors();

            if (UI.TreeView.SelectedNode != null || ShowSelectionStatus)
            {
                UI.StatusLabel.Text = Selection.Summary(true) + " selected.";
                ShowSelectionStatus = true;
            }

            DynamicMenu_Update();
        }

        bool ShowSelectionStatus = false;

        #endregion
        
        private void TreeView_Leave(object sender, EventArgs e)
        {
            if (UI.TreeView.CurrentEditor != null)
            {
                UI.TreeView.HideEditor(false);
                UI.TreeView.Refresh();
            }
        }

        private void TreeView_NodeMouseDoubleClick(object sender, TreeNodeAdvMouseEventArgs e)
        {
            if(e.Node.Tag is IDAXExpressionObject)
                ExpressionEditor_Edit(e.Node.Tag as IDAXExpressionObject);
        }

        private void TreeView_ItemDrag(object sender, ItemDragEventArgs e)
        {
            Tree_CurrentDragObject = new DataObject();

            draggedNodes = UI.TreeView.SelectedNodes.Contains(UI.TreeView.CurrentNode) ? UI.TreeView.SelectedNodes.ToArray() : new[] { UI.TreeView.CurrentNode };

            var scriptableObjects = new HashSet<ObjectType>() { ObjectType.Table, ObjectType.Role, ObjectType.DataSource, ObjectType.Partition };

            // Only generate TMSL script when dragging a single object:
            if (draggedNodes.Length == 1 && UI.TreeView.CurrentNode.Tag is Model) Tree_CurrentDragObject.SetData(Scripter.ScriptCreateOrReplace());
            else if (draggedNodes.Length == 1 && UI.TreeView.CurrentNode.Tag is LogicalGroup)
            {
                Tree_CurrentDragObject.SetData(Serializer.SerializeObjects(UI.TreeView.CurrentNode.Children.Select(n => n.Tag).OfType<TabularObject>()));
            }
            else if (draggedNodes.Length == 1 && UI.TreeView.CurrentNode.Tag is TabularNamedObject && scriptableObjects.Contains((UI.TreeView.CurrentNode.Tag as TabularNamedObject).ObjectType))
                Tree_CurrentDragObject.SetData(Scripter.ScriptCreateOrReplace(UI.TreeView.CurrentNode.Tag as TabularNamedObject));
            else Tree_CurrentDragObject.SetData(Serializer.SerializeObjects(Selection.OfType<TabularObject>()));

            Tree_CurrentDragObject.SetData(draggedNodes);
            UI.TreeView.DoDragDrop(Tree_CurrentDragObject, DragDropEffects.Move | DragDropEffects.Copy);
            draggedNodes = null;
        }

        private void TreeView_DragOver(object sender, DragEventArgs e)
        {
            if (Handler == null) return;

            e.Effect = DragDropEffects.None;

            if (draggedNodes != null && UI.TreeView.DropPosition.Node != null)
            {
                TreeNodeAdv dropTarget = UI.TreeView.DropPosition.Node;

                if (Tree.CanDrop(draggedNodes, dropTarget, UI.TreeView.DropPosition.Position)) e.Effect = DragDropEffects.Move;
            }
        }

        private void TreeView_DragDrop(object sender, DragEventArgs e)
        {
            if (Handler == null) return;

            if (draggedNodes != null && UI.TreeView.DropPosition.Node != null)
            {
                // TODO: Handle this through actions
                TreeNodeAdv dropTarget = UI.TreeView.DropPosition.Node;
                Tree.DoDrop(draggedNodes, dropTarget, UI.TreeView.DropPosition.Position);

                if (!dropTarget.IsLeaf) dropTarget.Expand();

                /*
                UI.TreeView.SuspendSelectionEvent = true;
                UI.TreeView.ClearSelectionInternal();
                UI.TreeView.Selection.AddRange(draggedNodes);
                UI.TreeView.SuspendSelectionEvent = false;*/
            }

            Tree_CurrentDragObject = null;
            Tree_DragBackup = null;
            draggedNodes = null;
        }

        private void TvName_ChangesApplied(object sender, EventArgs e)
        {
            UI.PropertyGrid.Refresh();
        }

        private LogicalTreeOptions CurrentOptions = LogicalTreeOptions.Default;

        public void SetDisplayOptions(bool showHidden, bool showDisplayFolders, bool showColumns, 
            bool showMeasures, bool showHierarchies, bool showAllObjectTypes, bool alphabeticalSort, string filter = null)
        {
            CurrentOptions = 
                (showHidden ? LogicalTreeOptions.ShowHidden : 0) |
                (showDisplayFolders ? LogicalTreeOptions.DisplayFolders : 0) |
                (showColumns ? LogicalTreeOptions.Columns : 0) |
                (showMeasures ? LogicalTreeOptions.Measures : 0) |
                (showHierarchies ? LogicalTreeOptions.Hierarchies : 0) |
                (showAllObjectTypes ? LogicalTreeOptions.AllObjectTypes : 0) |
                LogicalTreeOptions.ShowRoot;

            if (Tree == null) return;

            var cmp = (UI.TreeView.Model as SortedTreeModel)?.Comparer as TabularObjectComparer;
            if(cmp != null)
            {
                cmp.Order = alphabeticalSort ? ObjectOrder.Alphabetical : ObjectOrder.Metadata;
            }

            Tree.Options = CurrentOptions;
            Tree.Filter = filter;
        }
    }
}
