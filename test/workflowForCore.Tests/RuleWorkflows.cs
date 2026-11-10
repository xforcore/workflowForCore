// workflowForCore's own code (MIT). Licensed under the MIT license; see LICENSE.
//
// Rules: a declarative condition and a RuleSet, both from the workflow's .rules resource (as Visual Studio's designer
// writes them: <workflow type's full name>.rules, in WF3's markup), and a RuleSet built in code with CodeDom.
using System;
using System.CodeDom;
using System.Workflow.Activities;
using System.Workflow.Activities.Rules;
using System.Workflow.ComponentModel;

namespace WorkflowForCore.Tests
{
    // RulesWorkflow.rules has the condition IsLarge (Amount > 100) and the RuleSet Pricing (discount and shipping).
    public sealed class RulesWorkflow : SequentialWorkflowActivity
    {
        public int Amount { get; set; }
        public int Discount { get; set; }
        public string Shipping { get; set; }
        public string Size { get; set; }

        public RulesWorkflow()
        {
            var ifElse = new IfElseActivity("sizeCheck");
            var large = new IfElseBranchActivity("large");
            large.Condition = new RuleConditionReference { ConditionName = "IsLarge" };
            var setLarge = new CodeActivity("setLarge");
            setLarge.ExecuteCode += OnLarge;
            large.Activities.Add(setLarge);
            var small = new IfElseBranchActivity("small");
            var setSmall = new CodeActivity("setSmall");
            setSmall.ExecuteCode += OnSmall;
            small.Activities.Add(setSmall);
            ifElse.Activities.Add(large);
            ifElse.Activities.Add(small);
            Activities.Add(ifElse);

            Activities.Add(new PolicyActivity("pricing") { RuleSetReference = new RuleSetReference("Pricing") });
        }

        void OnLarge(object sender, EventArgs e) => Size = "large";
        void OnSmall(object sender, EventArgs e) => Size = "small";
    }

    // The same RuleSet, built with CodeDom and set on the workflow in code, with a While on a code-built condition.
    public sealed class CodeRulesWorkflow : SequentialWorkflowActivity
    {
        public int Amount { get; set; }
        public int Discount { get; set; }
        public string Shipping { get; set; }
        public int Count { get; set; }

        public CodeRulesWorkflow()
        {
            var definitions = new RuleDefinitions();
            definitions.RuleSets.Add(RulesInCode.Pricing());
            definitions.Conditions.Add(new RuleExpressionCondition("BelowThree",
                new CodeBinaryOperatorExpression(This("Count"), CodeBinaryOperatorType.LessThan, new CodePrimitiveExpression(3))));
            SetValue(RuleDefinitions.RuleDefinitionsProperty, definitions);

            Activities.Add(new PolicyActivity("pricing") { RuleSetReference = new RuleSetReference("Pricing") });
            var loop = new WhileActivity("loop") { Condition = new RuleConditionReference { ConditionName = "BelowThree" } };
            var body = new CodeActivity("count");
            body.ExecuteCode += OnCount;
            loop.Activities.Add(body);
            Activities.Add(loop);
        }

        void OnCount(object sender, EventArgs e) => Count++;

        static CodePropertyReferenceExpression This(string property) => new CodePropertyReferenceExpression(new CodeThisReferenceExpression(), property);
    }

    public static class RulesInCode
    {
        static CodePropertyReferenceExpression This(string property) => new CodePropertyReferenceExpression(new CodeThisReferenceExpression(), property);

        // Discount: IF Amount > 100 THEN Discount = 10 ELSE Discount = 0 (priority 2).
        // Shipping: IF Discount > 0 THEN Shipping = "free" ELSE Shipping = "paid" (priority 1; runs after, and again
        // if Discount changes: forward chaining).
        public static RuleSet Pricing()
        {
            var set = new RuleSet("Pricing");
            var discount = new Rule("Discount") { Priority = 2 };
            discount.Condition = new RuleExpressionCondition(new CodeBinaryOperatorExpression(This("Amount"), CodeBinaryOperatorType.GreaterThan, new CodePrimitiveExpression(100)));
            discount.ThenActions.Add(new RuleStatementAction(new CodeAssignStatement(This("Discount"), new CodePrimitiveExpression(10))));
            discount.ElseActions.Add(new RuleStatementAction(new CodeAssignStatement(This("Discount"), new CodePrimitiveExpression(0))));
            set.Rules.Add(discount);
            var shipping = new Rule("Shipping") { Priority = 1 };
            shipping.Condition = new RuleExpressionCondition(new CodeBinaryOperatorExpression(This("Discount"), CodeBinaryOperatorType.GreaterThan, new CodePrimitiveExpression(0)));
            shipping.ThenActions.Add(new RuleStatementAction(new CodeAssignStatement(This("Shipping"), new CodePrimitiveExpression("free"))));
            shipping.ElseActions.Add(new RuleStatementAction(new CodeAssignStatement(This("Shipping"), new CodePrimitiveExpression("paid"))));
            set.Rules.Add(shipping);
            return set;
        }
    }
}
