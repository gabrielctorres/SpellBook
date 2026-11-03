using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Core;
using SpellBook.Testing;
using UnityEngine;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// <see cref="ActionContext.CreateDerived"/> copia o <c>CustomData</c> (NC6, CA18 a CA20), e o comparador das chaves
    /// vem de <see cref="ActionContext.SetCustomDataComparer"/>.
    /// </summary>
    public class ActionContextTests
    {
        private static ActionContext CreateContext()
        {
            return new ActionContext();
        }

        // Ability e Level têm setter internal e o GAS não expõe internals aos testes.
        private static void SetInternal(ActionContext context, string property, object value)
        {
            typeof(ActionContext).GetProperty(property).SetValue(context, value);
        }

        [Test]
        public void CreateDerived_SeesOriginalKeysAndValues()
        {
            ActionContext original = CreateContext();
            original.CustomData["crit"] = true;
            original.CustomData["hits"] = 3;

            ActionContext derived = original.CreateDerived(Vector3.one);

            Assert.That(derived.CustomData["crit"], Is.EqualTo(true));
            Assert.That(derived.CustomData["hits"], Is.EqualTo(3));
            Assert.That(derived.CustomData.Count, Is.EqualTo(2));
        }

        [Test]
        public void CreateDerived_AddingKeyInDerived_DoesNotChangeOriginal()
        {
            ActionContext original = CreateContext();
            original.CustomData["crit"] = true;
            ActionContext derived = original.CreateDerived(Vector3.one);

            derived.CustomData["impact"] = 1;
            derived.CustomData["crit"] = false;

            Assert.That(original.CustomData.ContainsKey("impact"), Is.False);
            Assert.That(original.CustomData["crit"], Is.EqualTo(true));
        }

        [Test]
        public void CreateDerived_AddingKeyInOriginal_DoesNotChangeDerived()
        {
            ActionContext original = CreateContext();
            original.CustomData["crit"] = true;
            ActionContext derived = original.CreateDerived(Vector3.one);

            original.CustomData["late"] = 1;
            original.CustomData["crit"] = false;

            Assert.That(derived.CustomData.ContainsKey("late"), Is.False);
            Assert.That(derived.CustomData["crit"], Is.EqualTo(true));
        }

        [Test]
        public void CreateDerived_SharesValueReferences()
        {
            ActionContext original = CreateContext();
            var shared = new List<int> { 1 };
            original.CustomData["list"] = shared;

            ActionContext derived = original.CreateDerived(Vector3.one);

            Assert.That(derived.CustomData["list"], Is.SameAs(shared));
        }

        [Test]
        public void CreateDerived_KeepsComparer()
        {
            ActionContext original = CreateContext();
            original.SetCustomDataComparer(StringComparer.OrdinalIgnoreCase);
            original.CustomData["Crit"] = true;

            ActionContext derived = original.CreateDerived(Vector3.one);

            Assert.That(derived.CustomData.Comparer, Is.SameAs(StringComparer.OrdinalIgnoreCase));
            Assert.That(derived.CustomData.ContainsKey("crit"), Is.True);
        }

        [Test]
        public void CreateDerived_KeepsSourceAbilityAndLevel()
        {
            using var gas = new GasFactory();
            GameplayAbility ability = gas.CreateAbility();
            ActionContext original = CreateContext();
            SetInternal(original, nameof(ActionContext.Ability), ability);
            SetInternal(original, nameof(ActionContext.Level), 4);

            ActionContext derived = original.CreateDerived(new Vector3(1f, 2f, 3f));

            Assert.That(derived.Instigator, Is.SameAs(original.Instigator));
            Assert.That(derived.Ability, Is.SameAs(ability));
            Assert.That(derived.Level, Is.EqualTo(4));
            Assert.That(derived.Point, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(derived.Spec, Is.Null);
            Assert.That(derived.IsCancelled, Is.False);
        }

        [Test]
        public void SetCustomDataComparer_WithKeys_Throws()
        {
            ActionContext context = CreateContext();
            context.CustomData["crit"] = true;

            Assert.Throws<InvalidOperationException>(() => context.SetCustomDataComparer(StringComparer.OrdinalIgnoreCase));
            Assert.That(context.CustomData.Comparer, Is.SameAs(EqualityComparer<string>.Default), "o comparador mudou mesmo assim");
        }

        [Test]
        public void SetCustomDataComparer_Null_UsesDefault()
        {
            ActionContext context = CreateContext();
            context.SetCustomDataComparer(StringComparer.OrdinalIgnoreCase);

            context.SetCustomDataComparer(null);
            context.CustomData["Crit"] = true;

            Assert.That(context.CustomData.Comparer, Is.SameAs(EqualityComparer<string>.Default));
            Assert.That(context.CustomData.ContainsKey("crit"), Is.False);
        }

        [Test]
        public void PooledContext_AfterCustomComparer_ComesBackWithDefault()
        {
            using var gas = new GasFactory();
            AbilitySystemComponent asc = gas.CreateAsc();
            var first = new CaptureComparerAction();
            var second = new CaptureComparerAction();
            ActionContext custom = CreateContext();
            custom.SetCustomDataComparer(StringComparer.OrdinalIgnoreCase);

            asc.RunActions(new GameplayAction[] { first }, custom, null);
            asc.RunActions(new GameplayAction[] { second }, null, null);

            Assert.That(first.Comparer, Is.SameAs(StringComparer.OrdinalIgnoreCase));
            Assert.That(second.Context, Is.SameAs(custom), "a segunda execução não usou o contexto do pool");
            Assert.That(second.Comparer, Is.SameAs(EqualityComparer<string>.Default));
        }

        private sealed class CaptureComparerAction : GameplayAction
        {
            public ActionContext Context;
            public IEqualityComparer<string> Comparer;

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                Context = context;
                Comparer = context.CustomData.Comparer;
                return ActionStatus.Done;
            }
        }
    }
}
