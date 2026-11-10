// workflowForCore's own code (MIT). Licensed under the MIT license; see LICENSE.
//
// Rules (System.Workflow.Activities.Rules): declarative conditions and RuleSets from a workflow's .rules resource (WF3's
// markup, read with WorkflowMarkupSerializer) and built in code; PolicyActivity's forward chaining; RuleSet's own
// validation and execution outside a workflow; and markup written by this runtime read back. The same on .NET
// Framework 4.8.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Workflow.Activities.Rules;
using System.Workflow.ComponentModel.Compiler;
using System.Workflow.ComponentModel.Serialization;
using System.Xml;
using Xunit;

namespace WorkflowForCore.Tests
{
    public class RulesTests
    {
        public RulesTests() => Trace.Reset();

        static Dictionary<string, object> Run(Type type, int amount)
        {
            using (var host = new Host())
            {
                host.Run(type, new Dictionary<string, object> { { "Amount", amount } });
                Assert.Equal("Completed", host.Events.Last());
                return host.Completed.OutputParameters;
            }
        }

        [Theory]
        [InlineData(150, "large", 10, "free")]
        [InlineData(100, "small", 0, "paid")]
        public void Rules_from_the_rules_resource_drive_IfElse_and_PolicyActivity(int amount, string size, int discount, string shipping)
        {
            Dictionary<string, object> outputs = Run(typeof(RulesWorkflow), amount);
            Assert.Equal(size, outputs["Size"]);
            Assert.Equal(discount, outputs["Discount"]);
            Assert.Equal(shipping, outputs["Shipping"]);
        }

        [Fact]
        public void Rules_built_in_code_drive_PolicyActivity_and_While()
        {
            Dictionary<string, object> outputs = Run(typeof(CodeRulesWorkflow), 500);
            Assert.Equal(10, outputs["Discount"]);
            Assert.Equal("free", outputs["Shipping"]);
            Assert.Equal(3, outputs["Count"]);
        }

        public sealed class Order
        {
            public int Amount { get; set; }
            public int Discount { get; set; }
            public string Shipping { get; set; }
        }

        [Fact]
        public void A_RuleSet_validates_and_executes_against_an_object()
        {
            RuleSet set = RulesInCode.Pricing();
            var validation = new RuleValidation(typeof(Order), null);
            Assert.True(set.Validate(validation));
            Assert.Empty(validation.Errors);
            var order = new Order { Amount = 101 };
            set.Execute(new RuleExecution(validation, order));
            Assert.Equal(10, order.Discount);
            Assert.Equal("free", order.Shipping);
        }

        [Fact]
        public void Forward_chaining_reruns_a_rule_when_a_later_rule_changes_what_it_read()
        {
            // Shipping (priority 1) reads Discount; a higher-priority rule sets Discount, so Shipping sees the new one.
            RuleSet set = RulesInCode.Pricing();
            var validation = new RuleValidation(typeof(Order), null);
            set.Validate(validation);
            var order = new Order { Amount = 5, Discount = 99 };
            set.Execute(new RuleExecution(validation, order));
            Assert.Equal(0, order.Discount);
            Assert.Equal("paid", order.Shipping);
        }

        [Fact]
        public void A_rule_on_an_unknown_property_doesnt_validate()
        {
            var set = new RuleSet("bad");
            var rule = new Rule("unknown");
            rule.Condition = new RuleExpressionCondition(new System.CodeDom.CodeBinaryOperatorExpression(
                new System.CodeDom.CodePropertyReferenceExpression(new System.CodeDom.CodeThisReferenceExpression(), "NoSuchProperty"),
                System.CodeDom.CodeBinaryOperatorType.ValueEquality, new System.CodeDom.CodePrimitiveExpression(1)));
            set.Rules.Add(rule);
            var validation = new RuleValidation(typeof(Order), null);
            Assert.False(set.Validate(validation));
            ValidationError error = validation.Errors.Single();
            Assert.Contains("NoSuchProperty", error.ErrorText);
        }

        [Fact]
        public void A_RuleSet_prints_as_text()
        {
            Rule discount = RulesInCode.Pricing().Rules.Single(r => r.Name == "Discount");
            Assert.Equal("this.Amount > 100", discount.Condition.ToString());
            Assert.Equal("this.Discount = 10", discount.ThenActions.Single().ToString());
        }

        [Fact]
        public void Rule_definitions_written_as_markup_read_back()
        {
            var definitions = new RuleDefinitions();
            definitions.RuleSets.Add(RulesInCode.Pricing());
            var text = new StringBuilder();
            using (XmlWriter writer = XmlWriter.Create(new StringWriter(text), new XmlWriterSettings { Indent = true }))
                new WorkflowMarkupSerializer().Serialize(writer, definitions);
            Assert.Contains("Name=\"Pricing\"", text.ToString());

            RuleDefinitions read;
            using (XmlReader reader = XmlReader.Create(new StringReader(text.ToString())))
                read = (RuleDefinitions)new WorkflowMarkupSerializer().Deserialize(reader);
            RuleSet set = read.RuleSets["Pricing"];
            Assert.Equal(new[] { "Discount", "Shipping" }, set.Rules.Select(r => r.Name).OrderBy(n => n));
            var validation = new RuleValidation(typeof(Order), null);
            Assert.True(set.Validate(validation));
            var order = new Order { Amount = 200 };
            set.Execute(new RuleExecution(validation, order));
            Assert.Equal("free", order.Shipping);
        }
    }
}
