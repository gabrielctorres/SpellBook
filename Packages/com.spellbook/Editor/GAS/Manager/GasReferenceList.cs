using System;
using System.Collections.Generic;
using SpellBook.Core.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Editor
{
    /// <summary>Uma linha do painel de referências: o objeto que o clique destaca, o nome e onde ele está.</summary>
    internal readonly struct GasReferenceRow
    {
        public readonly Object Target;
        public readonly string Name;

        /// <summary>"Asset", "Prefab" ou "Cena aberta: &lt;cena&gt;" (DS 5.4).</summary>
        public readonly string Location;

        public GasReferenceRow(Object target, string name, string location)
        {
            Target = target;
            Name = name;
            Location = location;
        }
    }

    /// <summary>
    /// Linhas dos grupos "Usa" e "Usado por" do gerenciador (ME8, CA9), pela busca do <see cref="ReferenceFinder"/>:
    /// uma por objeto. Um componente de prefab vira o prefab, e um de cena vira o GameObject dele.
    /// </summary>
    internal static class GasReferenceList
    {
        public const string ClosedScenesNote = "Cenas fechadas não entram nesta busca.";

        public static List<GasReferenceRow> UsedBy(Object asset)
        {
            var rows = new List<GasReferenceRow>();
            foreach (ReferenceUser user in ReferenceFinder.FindUsers(asset)) AddRow(rows, user.Owner);
            return rows;
        }

        public static List<GasReferenceRow> Uses(Object asset)
        {
            var rows = new List<GasReferenceRow>();
            foreach (Object used in ReferenceFinder.FindUses(asset)) AddRow(rows, used);
            return rows;
        }

        private static void AddRow(List<GasReferenceRow> rows, Object obj)
        {
            GasReferenceRow row = Describe(obj);
            if (row.Target == null || rows.Exists(r => r.Target == row.Target)) return;
            rows.Add(row);
        }

        private static GasReferenceRow Describe(Object obj)
        {
            if (obj is Component component) obj = component.gameObject;
            if (obj is GameObject gameObject && !EditorUtility.IsPersistent(gameObject))
            {
                string scene = gameObject.scene.name.Length > 0 ? gameObject.scene.name : "sem título";
                return new GasReferenceRow(gameObject, gameObject.name, "Cena aberta: " + scene);
            }

            string path = AssetDatabase.GetAssetPath(obj);
            if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                Object prefab = AssetDatabase.LoadMainAssetAtPath(path);
                return new GasReferenceRow(prefab, prefab.name, "Prefab");
            }

            return new GasReferenceRow(obj, obj.name, "Asset");
        }
    }
}
