using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SceneChanger : MonoBehaviour
{
    public void NextScene()
    {
        SceneManager.LoadScene(3);
    }
}
