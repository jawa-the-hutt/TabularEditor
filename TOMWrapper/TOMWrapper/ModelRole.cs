﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TabularEditor.PropertyGridUI;
using TOM = Microsoft.AnalysisServices.Tabular;

namespace TabularEditor.TOMWrapper
{
    public partial class ModelRole
    {
        [Browsable(true), DisplayName("Row Level Security"), Category("Security")]
        public RoleRLSIndexer RowLevelSecurity { get; private set; }

#if CL1400
        [Browsable(true), DisplayName("Object Level Security"), Category("Security")]
        public RoleOLSIndexer MetadataPermission { get; private set; }
#endif

        public void InitRLSIndexer()
        {
            RowLevelSecurity = new RoleRLSIndexer(this);
#if CL1400            
            MetadataPermission = new RoleOLSIndexer(this);
#endif
        }

        /*public override TabularNamedObject Clone(string newName, bool includeTranslations)
        {
            Handler.BeginUpdate("duplicate role");
            var tom = MetadataObject.Clone();
            //tom.IsRemoved = false;
            tom.Name = Model.Roles.MetadataObjectCollection.GetNewName(string.IsNullOrEmpty(newName) ? tom.Name + " copy" : newName);
            var r = new ModelRole(Handler, tom);
            r.InitRLSIndexer();
            Model.Roles.Add(r);

            if (includeTranslations)
            {
                r.TranslatedDescriptions.CopyFrom(TranslatedDescriptions);
                r.TranslatedDisplayFolders.CopyFrom(TranslatedDisplayFolders);
                if (string.IsNullOrEmpty(newName))
                    r.TranslatedNames.CopyFrom(TranslatedNames, n => n + " copy");
                else
                    r.TranslatedNames.CopyFrom(TranslatedNames, n => n.Replace(Name, newName));
            }

            Handler.EndUpdate();

            return r;
        }*/

        [Category("Security"), DisplayName("Members")]
        [Description("Specify domain/usernames of the members in this role. One member per line.")]
        [Editor(typeof(System.ComponentModel.Design.MultilineStringEditor), typeof(System.Drawing.Design.UITypeEditor))]
        public string RoleMembers
        {
            get
            {
                return string.Join("\r\n", MetadataObject.Members.Select(m => m.MemberName));
            }
            set
            {
                if (MetadataObject.Members.Any(m => m is TOM.ExternalModelRoleMember))
                    throw new InvalidOperationException("This role uses External Role Members. These role members are not supported in this version of Tabular Editor.");
                if (RoleMembers == value) return;

                Handler.UndoManager.Add(new UndoFramework.UndoPropertyChangedAction(this, "RoleMembers", RoleMembers, value));
                MetadataObject.Members.Clear();
                foreach (var member in value.Replace("\r", "").Split('\n'))
                {
                    MetadataObject.Members.Add(new TOM.WindowsModelRoleMember() { MemberName = member });
                }
            }
        }

        protected override bool IsBrowsable(string propertyName)
        {
            switch (propertyName) {
                case "MetadataPermission": return Model.Database.CompatibilityLevel >= 1400;
                default:  return true;
            }
        }
    }
}
