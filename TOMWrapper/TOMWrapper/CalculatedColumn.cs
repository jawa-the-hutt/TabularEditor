﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TabularEditor.TOMWrapper.Utils;
using TOM = Microsoft.AnalysisServices.Tabular;

namespace TabularEditor.TOMWrapper
{
    public partial class CalculatedColumn
    {
        [Browsable(false)]
        public Dictionary<IDaxObject, List<Dependency>> Dependencies { get; } = new Dictionary<IDaxObject, List<Dependency>>();

        protected override void OnPropertyChanged(string propertyName, object oldValue, object newValue)
        {
            if (propertyName == Properties.EXPRESSION)
            {
                NeedsValidation = true;
                FormulaFixup.BuildDependencyTree(this);
            }

            base.OnPropertyChanged(propertyName, oldValue, newValue);
        }


        [Browsable(false)]
        public bool NeedsValidation { get; set; }

        /*public TabularNamedObject CloneTo(Table table, string newName = null, bool includeTranslations = true)
        {
            Handler.BeginUpdate("duplicate calculated column");
            var tom = MetadataObject.Clone() as TOM.CalculatedColumn;
            //tom.IsRemoved = false;
            tom.Name = table.Columns.MetadataObjectCollection.GetNewName(string.IsNullOrEmpty(newName) ? tom.Name + " copy" : newName);
            var c = new CalculatedColumn(tom);
            table.Columns.Add(c);

#if CL1400
            c.InitOLSIndexer();
#endif

            if (includeTranslations)
            {
                c.TranslatedDescriptions.CopyFrom(TranslatedDescriptions);
                c.TranslatedDisplayFolders.CopyFrom(TranslatedDisplayFolders);
                if (string.IsNullOrEmpty(newName))
                    c.TranslatedNames.CopyFrom(TranslatedNames, n => n + " copy");
                else
                    c.TranslatedNames.CopyFrom(TranslatedNames, n => n.Replace(Name, newName));
            }
            c.InPerspective.CopyFrom(InPerspective);

            Handler.EndUpdate();

            return c;
        }*/

        /*public override TabularNamedObject Clone(string newName = null, bool includeTranslations = true)
        {
            return CloneTo(Table, newName, includeTranslations);
        }*/
    }
}
