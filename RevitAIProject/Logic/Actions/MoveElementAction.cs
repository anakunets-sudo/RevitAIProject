using Autodesk.Revit.DB;
using RevitAIProject.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace RevitAIProject.Logic.Actions
{
    [AiParam("MoveElement", Description = "Moves elements along X, Y, Z axes using millimeters.")]
    public class MoveElementAction : BaseRevitAction
    {
        [AiParam("MoveElement", Description = "Moves elements along X, Y, Z axes using millimeters.")]
        public string Target_ai_name { get; set; }

        [AiParam("dx", Description = "X-offset in mm")]
        public double Dx { get; set; }

        [AiParam("dy", Description = "Y-offset in mm")]
        public double Dy { get; set; }

        [AiParam("dz", Description = "Z-offset in mm")]
        public double Dz { get; set; }

        protected override void Execute(IRevitContext context)
        {
            using (Transaction tr = new Transaction(context.UIDoc.Document, TransactionName))
            {
                tr.Start();
                // Просто вызываем метод из базового класса!
                var targets = ResolveTargets(context);

                XYZ vector = new XYZ(Dx, Dy, Dz);
                foreach (var id in targets)
                    ElementTransformUtils.MoveElement(context.UIDoc.Document, id, vector);
                tr.Commit();
            }
        }
    }
}
