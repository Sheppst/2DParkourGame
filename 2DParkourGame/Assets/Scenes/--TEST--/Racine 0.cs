using UnityEngine;

// Je te demande d'au moins me sécuriser à peu plus ça, là c'est pour l'exemple

public class Racine0 : MonoBehaviour
{
    public static Racine0 R;
    public Racine10 R_1_0;
    public Racine11 R_1_1;

    private void Awake()
    {
        R = this;
        R_1_0.Awake();
        R_1_1.Awake();
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //Tu me fais pareil, que dans le awake bien sur avec les méthodes qui vont
    }

    // Update is called once per frame
    void Update()
    {
        //idem ici ou pour la fixe, et tout le reste
    }
}
