using System;
using UnityEngine;

[Serializable]
public class Racine11
{
    // là j'ai la flemme de créer plusieur class mais tu comprend 
    /// <summary>
    ///  Je l'ai mit en { get; set; } pour que ça soit modifiable car j'ai mit un [Serializable] pour modifier les propriété mais pas ça, ça reste accessible pour y manipuler les variables depuis le code
    /// </summary>
    public Racine21 R_2_1 { get; set; }
    internal void Awake()
    {
        //Puis comme dit dans la racine 0
        R_2_1.Awake();
    }
}
