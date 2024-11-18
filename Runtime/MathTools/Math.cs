using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpellBook.MathTools
{
    public class Math : MonoBehaviour
    {
        public static class MathTools
        {
            /// <summary>
            /// Calcula o ângulo entre dois vetores 2D em graus.
            /// </summary>
            /// <param name="from">O vetor inicial.</param>
            /// <param name="to">O vetor final.</param>
            /// <returns>O ângulo em graus entre os vetores.</returns>
            public static float AngleBetweenVectors(Vector2 from, Vector2 to)
            {
                return Vector2.SignedAngle(from, to);
            }

            /// <summary>
            /// Mapeia um valor de um intervalo para outro.
            /// </summary>
            /// <param name="value">O valor a ser mapeado.</param>
            /// <param name="inMin">O limite inferior do intervalo de entrada.</param>
            /// <param name="inMax">O limite superior do intervalo de entrada.</param>
            /// <param name="outMin">O limite inferior do intervalo de saída.</param>
            /// <param name="outMax">O limite superior do intervalo de saída.</param>
            /// <returns>O valor mapeado no intervalo de saída.</returns>
            public static float Map(float value, float inMin, float inMax, float outMin, float outMax)
            {
                return outMin + ((value - inMin) / (inMax - inMin)) * (outMax - outMin);
            }

            /// <summary>
            /// Calcula a projeção de um ponto no plano XZ para um ponto com altura ajustada (plano Y).
            /// </summary>
            /// <param name="point">O ponto no espaço.</param>
            /// <param name="height">A altura fixa (Y) para o ponto projetado.</param>
            /// <returns>O ponto projetado no plano XZ com altura fixa.</returns>
            public static Vector3 ProjectToXZPlane(Vector3 point, float height)
            {
                return new Vector3(point.x, height, point.z);
            }

            /// <summary>
            /// Gera um ponto aleatório dentro de um círculo em 2D.
            /// </summary>
            /// <param name="center">O centro do círculo.</param>
            /// <param name="radius">O raio do círculo.</param>
            /// <returns>Um ponto aleatório dentro do círculo.</returns>
            public static Vector2 RandomPointInCircle(Vector2 center, float radius)
            {
                float angle = Random.Range(0f, Mathf.PI * 2);
                float distance = Mathf.Sqrt(Random.Range(0f, 1f)) * radius; // Distribuição uniforme
                return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            }

            /// <summary>
            /// Gera um ponto aleatório dentro de uma esfera em 3D.
            /// </summary>
            /// <param name="center">O centro da esfera.</param>
            /// <param name="radius">O raio da esfera.</param>
            /// <returns>Um ponto aleatório dentro da esfera.</returns>
            public static Vector3 RandomPointInSphere(Vector3 center, float radius)
            {
                Vector3 randomPoint = Random.insideUnitSphere * radius;
                return center + randomPoint;
            }

            /// <summary>
            /// Calcula o ponto médio entre dois pontos no espaço.
            /// </summary>
            /// <param name="pointA">O primeiro ponto.</param>
            /// <param name="pointB">O segundo ponto.</param>
            /// <returns>O ponto médio.</returns>
            public static Vector3 MidPoint(Vector3 pointA, Vector3 pointB)
            {
                return (pointA + pointB) / 2f;
            }

            /// <summary>
            /// Calcula se um valor está dentro de um intervalo.
            /// </summary>
            /// <param name="value">O valor a ser testado.</param>
            /// <param name="min">O limite inferior do intervalo.</param>
            /// <param name="max">O limite superior do intervalo.</param>
            /// <returns>True se o valor estiver no intervalo; caso contrário, false.</returns>
            public static bool IsInRange(float value, float min, float max)
            {
                return value >= min && value <= max;
            }

            /// <summary>
            /// Calcula o centro de massa de uma lista de pontos.
            /// </summary>
            /// <param name="points">A lista de pontos.</param>
            /// <returns>O centro de massa calculado.</returns>
            public static Vector3 CenterOfMass(Vector3[] points)
            {
                Vector3 sum = Vector3.zero;
                foreach (var point in points)
                {
                    sum += point;
                }
                return sum / points.Length;
            }
        }
    }
}



