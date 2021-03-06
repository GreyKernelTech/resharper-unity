﻿using JetBrains.ReSharper.Daemon.Stages.Dispatcher;
using JetBrains.ReSharper.Feature.Services.Daemon;
using JetBrains.ReSharper.Plugins.Unity.Daemon.Stages.Dispatcher;
using JetBrains.ReSharper.Plugins.Unity.Daemon.Stages.Highlightings;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp.Tree;

namespace JetBrains.ReSharper.Plugins.Unity.Daemon.Stages.Analysis
{
    [ElementProblemAnalyzer(typeof(IAttribute),
        HighlightingTypes = new[]
        {
            typeof(RedundantInitializeOnLoadAttributeWarning),
            typeof(InvalidStaticModifierWarning),
            typeof(InvalidReturnTypeWarning),
            typeof(InvalidSignatureWarning)
        })]
    public class InitializeOnLoadSignatureProblemAnalyzer : UnityElementProblemAnalyzer<IAttribute>
    {
        private readonly IPredefinedTypeCache myPredefinedTypeCache;

        public InitializeOnLoadSignatureProblemAnalyzer(UnityApi unityApi, IPredefinedTypeCache predefinedTypeCache)
            : base(unityApi)
        {
            myPredefinedTypeCache = predefinedTypeCache;
        }

        protected override void Analyze(IAttribute element, ElementProblemAnalyzerData data, IHighlightingConsumer consumer)
        {
            var attributeTypeElement = element.TypeReference?.Resolve().DeclaredElement as ITypeElement;
            if (attributeTypeElement == null)
                return;

            if (Equals(attributeTypeElement.GetClrName(), KnownTypes.InitializeOnLoadAttribute))
            {
                var classLikeDeclaration = ClassLikeDeclarationNavigator.GetByAttribute(element);
                if (classLikeDeclaration != null && classLikeDeclaration.ConstructorDeclarations.All(c => !c.IsStatic))
                {
                    // Unity doesn't report a warning if there isn't a static constructor, so just highlight
                    // the attribute as dead code.
                    consumer.AddHighlighting(new RedundantInitializeOnLoadAttributeWarning(element));
                }
            }
            else if (Equals(attributeTypeElement.GetClrName(), KnownTypes.InitializeOnLoadMethodAttribute)
                || Equals(attributeTypeElement.GetClrName(), KnownTypes.RuntimeInitializeOnLoadMethodAttribute))
            {
                var methodDeclaration = MethodDeclarationNavigator.GetByAttribute(element);
                if (methodDeclaration == null)
                    return;

                var predefinedType = myPredefinedTypeCache.GetOrCreatePredefinedType(element.GetPsiModule());
                var methodSignature = new MethodSignature(predefinedType.Void, true);

                if (!methodDeclaration.IsStatic)
                    consumer.AddHighlighting(new InvalidStaticModifierWarning(methodDeclaration, methodSignature));
                if (!methodDeclaration.Type.IsVoid())
                    consumer.AddHighlighting(new InvalidReturnTypeWarning(methodDeclaration, methodSignature));
                if (methodDeclaration.ParameterDeclarationsEnumerable.Any())
                    consumer.AddHighlighting(new InvalidSignatureWarning(methodDeclaration, methodSignature));
            }
        }
    }
}