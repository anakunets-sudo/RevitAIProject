using Autodesk.Revit.DB;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace RevitAIProject.Logic.Queries
{
    public static class FilterJsonBuilder
    {
        public class FilterRuleDto
        {
            [JsonProperty("p")] public string ParameterName { get; set; }
            [JsonProperty("o")] public string Operator { get; set; }
            [JsonProperty("v")] public string Value { get; set; }
        }

        public static ElementFilter Build(Document doc, string json, BuiltInCategory category)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            var rules = JsonConvert.DeserializeObject<List<FilterRuleDto>>(json);
            if (rules == null || rules.Count == 0) return null;

            Element sample = new FilteredElementCollector(doc)
                .OfCategory(category)
                .WhereElementIsNotElementType()
                .FirstOrDefault();

            List<ElementFilter> filters = new List<ElementFilter>();

            foreach (var rule in rules)
            {
                Parameter param = FindParameterSmart(sample, rule.ParameterName);
                if (param == null) continue;

                ElementFilter ef = CreateFilterFromRule(param, rule);
                if (ef != null) filters.Add(ef);
            }

            if (filters.Count == 0) return null;
            return filters.Count > 1 ? new LogicalAndFilter(filters) : filters[0];
        }

        private static ElementFilter CreateFilterFromRule(Parameter param, FilterRuleDto dto)
        {
            StorageType type = param.StorageType;
            ParameterValueProvider provider = new ParameterValueProvider(param.Id);
            FilterRule revitRule = null;

            string op = dto.Operator.ToLower();
            bool isNotEquals = op == "notequals";

            if (type == StorageType.String)
            {
                FilterStringRuleEvaluator eval = MapStringEvaluator(op);
                revitRule = new FilterStringRule(provider, eval, dto.Value, false);
            }
            else if (type == StorageType.Double || type == StorageType.Integer)
            {
                double val;
                if (type == StorageType.Double) val = Services.LogicFactory.ParseToRevitFeet(dto.Value);
                else double.TryParse(dto.Value, out val);

                FilterNumericRuleEvaluator eval = MapNumericEvaluator(op);
                if (type == StorageType.Double)
                    revitRule = new FilterDoubleRule(provider, eval, val, 0.001);
                else
                    revitRule = new FilterIntegerRule(provider, eval, (int)val);
            }

            if (revitRule == null) return null;

            // РЕШЕНИЕ ДЛЯ 2019: Инверсия через конструктор
            return new ElementParameterFilter(revitRule, isNotEquals);
        }

        private static Parameter FindParameterSmart(Element element, string name)
        {
            if (element == null) return null;
            Parameter p = element.LookupParameter(name);
            if (p != null) return p;

            ElementId tid = element.GetTypeId();
            if (tid != ElementId.InvalidElementId)
            {
                Element te = element.Document.GetElement(tid);
                p = te?.LookupParameter(name);
                if (p != null) return p;
            }
            return element.Parameters.Cast<Parameter>()
                .FirstOrDefault(x => x.Definition.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        private static FilterStringRuleEvaluator MapStringEvaluator(string op)
        {
            switch (op)
            {
                case "contains": return new FilterStringContains();
                case "begins": return new FilterStringBeginsWith();
                case "ends": return new FilterStringEndsWith();
                default: return new FilterStringEquals();
            }
        }

        private static FilterNumericRuleEvaluator MapNumericEvaluator(string op)
        {
            switch (op)
            {
                case "greater": return new FilterNumericGreater();
                case "less": return new FilterNumericLess();
                default: return new FilterNumericEquals();
            }
        }
    }
}