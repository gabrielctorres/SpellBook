using SpellBook.Core;
using SpellBook.Core.Text;
using UnityEngine;
using UnityEngine.Serialization;

namespace SpellBook.GAS.Attributes
{
    /// <summary>Grupo de atributos para a interface (por exemplo, "Ofensivo" ou "Defensivo"), escolhido em <see cref="AttributeDisplay.Category"/>.</summary>
    [CreateAssetMenu(fileName = "NewAttributeCategory", menuName = "GAS/Attribute Category")]
    [HelpURL(DocUrls.Gas + "#attributecategory")]
    public class AttributeCategory : ScriptableObject, ISerializationCallbackReceiver
    {
        [Tooltip("Nome exibido da categoria, com chave opcional para o provedor de texto.")]
        [SerializeField] private LocalizedText localizedName;

        // Só leitura do formato 2.x ("DisplayName: texto"); convertido em OnAfterDeserialize (ADR 0009). Sai antes da 3.0 final.
        [FormerlySerializedAs("DisplayName")]
        [SerializeField, HideInInspector] private string legacyDisplayName;

        [Tooltip("Ordem da categoria na interface; menor vem primeiro")]
        public int Order;

        /// <summary>Nome exibido; resolva com <see cref="LocalizedText.Resolve"/> ou leia o texto padrão em <see cref="LocalizedText.Text"/>.</summary>
        public LocalizedText DisplayName
        {
            get => localizedName;
            set => localizedName = value;
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (string.IsNullOrEmpty(legacyDisplayName)) return;
            localizedName = new LocalizedText(null, legacyDisplayName);
            legacyDisplayName = null;
        }
    }
}
