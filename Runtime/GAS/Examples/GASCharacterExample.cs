using UnityEngine;
using SpellBook.GAS.Core;
using SpellBook.GAS.Abilities;

namespace SpellBook.GAS.Examples
{
    public class GASCharacterExample : MonoBehaviour
    {
        [SerializeField] private AbilitySystemComponent asc;
        [SerializeField] private GameplayAbility basicAttack;

        void Update()
        {
            // Exemplo: Ataca ao clicar no botão esquerdo
            if (Input.GetMouseButtonDown(0))
            {
                // Busca a posição do mouse no mundo (exemplo simplificado)
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    basicAttack.Activate(asc, hit.point);
                }
            }
        }
    }
}

