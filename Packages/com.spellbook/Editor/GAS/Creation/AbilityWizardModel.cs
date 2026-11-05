using System.Collections.Generic;
using System.IO;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Effects;
using UnityEditor;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Editor
{
    /// <summary>Uma linha da lista do que será criado (DS 4.2): um asset, uma tag ou um prefab mantido.</summary>
    internal sealed class AbilityWizardRow
    {
        /// <summary>O objeto que dá o ícone: o modelo, a tag existente ou o prefab; <c>null</c> numa tag nova.</summary>
        public Object Icon { get; internal set; }
        public string Name { get; internal set; }
        public string Type { get; internal set; }
        /// <summary>Ícone e palavra: "+ nova", "↺ já existe", "✖ colisão" ou "🔗 referência".</summary>
        public string Marker { get; internal set; }
        public bool IsMain { get; internal set; }
        public string Tooltip { get; internal set; }
    }

    /// <summary>
    /// Estado do assistente de criação, sem UI (DS 4): os quatro passos, os modelos do <see cref="AbilityTemplateCatalog"/>,
    /// o <see cref="AbilitySetPlan"/> do nome e da pasta, o motivo que desliga o botão do passo, a criação pelo
    /// <see cref="AbilitySetCreator"/> e o "Desfazer criação", que apaga o conjunto (a Undo da Unity não cobre os arquivos).
    /// </summary>
    internal sealed class AbilityWizardModel
    {
        internal const int StepCount = 4;
        internal const string ReferenceTooltip = "Este prefab é do pacote e é compartilhado. Para mudar o visual só desta "
            + "habilidade, duplique o prefab no projeto e troque a referência no Inspector.";

        private static readonly string[] Titles = { "Modelo", "Nome e pasta", "Revisão", "Resultado" };

        private readonly List<AbilityTemplate> _templates = new List<AbilityTemplate>();
        private readonly List<AbilityTemplate> _broken = new List<AbilityTemplate>();
        private int _createdAssets;

        public IReadOnlyList<AbilityTemplate> Templates => _templates;

        public int BrokenCount => _broken.Count;

        /// <summary>A linha do fim da lista de modelos (DS 7); <c>null</c> se nenhum está quebrado.</summary>
        public string BrokenNote => _broken.Count == 0 ? null
            : $"{_broken.Count} {(_broken.Count == 1 ? "modelo" : "modelos")} com asset ausente: veja SpellBook/Validar projeto";

        public int Step { get; private set; } = 1;

        public string StepTitle => Titles[Step - 1];

        public string StepLabel => $"Passo {Step} de {StepCount}";

        public AbilityTemplate Template { get; private set; }

        public string Name { get; private set; } = string.Empty;

        public string Folder { get; private set; } = string.Empty;

        /// <summary>O plano do modelo, do nome e da pasta; <c>null</c> sem modelo.</summary>
        public AbilitySetPlan Plan { get; private set; }

        /// <summary>O último conjunto criado, que o "Desfazer criação" apaga; <c>null</c> se não há.</summary>
        public CreatedSet Created { get; private set; }

        /// <summary>O que desliga "Avançar" ou "Criar" no passo atual; <c>null</c> se o botão está ligado.</summary>
        public string Reason => Step switch
        {
            1 => Template == null ? "Escolha um modelo." : null,
            2 => AbilitySetPlan.CheckName(Name) ?? AbilitySetPlan.CheckFolder(Folder),
            3 => Plan.Problem,
            _ => null,
        };

        public bool CanCreate => Step == 3 && Plan.CanCreate;

        public string ResultText => $"Conjunto criado: {_createdAssets} assets e {Created.Paths.Count - _createdAssets} tags";

        public string UndoConfirm => $"Apagar os {_createdAssets} assets e {Created.Paths.Count - _createdAssets} tags criados?";

        /// <summary>A frase do modelo; um modelo do projeto sem descrição diz "Modelo do projeto" (DS 4.3).</summary>
        public static string Describe(AbilityTemplate template) =>
            string.IsNullOrWhiteSpace(template.Description) ? "Modelo do projeto" : template.Description;

        /// <summary>A pasta do item selecionado no Project window: a própria pasta, a pasta do asset, ou <c>Assets</c>.</summary>
        public static string FolderOf(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return "Assets";
            return AssetDatabase.IsValidFolder(assetPath) ? assetPath : Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
        }

        public void Load() => AbilityTemplateCatalog.Find(_templates, _broken);

        public void SelectTemplate(AbilityTemplate template)
        {
            Template = template;
            Rebuild();
        }

        public void SetName(string name)
        {
            Name = name ?? string.Empty;
            Rebuild();
        }

        public void SetFolder(string folder)
        {
            Folder = folder ?? string.Empty;
            Rebuild();
        }

        public void Next()
        {
            if (Step < 3 && Reason == null) Step++;
        }

        /// <summary>Volta um passo; do resultado, volta ao nome para criar outro conjunto.</summary>
        public void Back()
        {
            if (Step == 4) Step = 2;
            else if (Step > 1) Step--;
        }

        /// <summary>Cria o conjunto, seleciona o asset principal e vai ao passo 4.</summary>
        public void Create()
        {
            if (!CanCreate) return;
            _createdAssets = Plan.Assets.Count;
            Created = AbilitySetCreator.Create(Plan);
            Step = 4;
            Rebuild();
        }

        /// <summary>Apaga o conjunto criado, sem as tags reutilizadas, e volta ao passo 2 com o mesmo nome.</summary>
        public void UndoCreation()
        {
            if (Created == null) return;
            AbilitySetCreator.Delete(Created);
            Created = null;
            Step = 2;
            Rebuild();
        }

        /// <summary>As linhas do plano: assets, tags e prefabs mantidos, nessa ordem.</summary>
        public List<AbilityWizardRow> Rows
        {
            get
            {
                var rows = new List<AbilityWizardRow>();
                if (Plan == null) return rows;
                foreach (PlannedAsset asset in Plan.Assets)
                {
                    rows.Add(new AbilityWizardRow
                    {
                        Icon = asset.Source, Name = asset.Name, Type = TypeName(asset.Source), Marker = Marker(asset.Status),
                        IsMain = asset.IsMain, Tooltip = asset.IsMain ? "Principal" : null,
                    });
                }

                foreach (PlannedTag tag in Plan.Tags)
                {
                    rows.Add(new AbilityWizardRow
                    {
                        Icon = tag.Existing, Name = tag.Name, Marker = Marker(tag.Status),
                        Type = tag.ParentName == null ? "Tag" : $"Tag (pai: {tag.ParentName})",
                    });
                }

                foreach (Object prefab in Plan.References)
                {
                    bool fromPackage = AssetDatabase.GetAssetPath(prefab).StartsWith("Packages/");
                    rows.Add(new AbilityWizardRow
                    {
                        Icon = prefab, Name = prefab.name, Type = fromPackage ? "Prefab do pacote" : "Prefab",
                        Marker = "🔗 referência", Tooltip = fromPackage ? ReferenceTooltip : null,
                    });
                }

                return rows;
            }
        }

        private void Rebuild() => Plan = Template != null ? AbilitySetPlan.Build(Template, Name, Folder) : null;

        private static string Marker(PlannedStatus status) =>
            status == PlannedStatus.New ? "+ nova" : status == PlannedStatus.Exists ? "↺ já existe" : "✖ colisão";

        private static string TypeName(Object source) =>
            source is GameplayAbility ? "Habilidade" : source is GameplayEffect ? "Efeito" : source != null ? source.GetType().Name : string.Empty;
    }
}
