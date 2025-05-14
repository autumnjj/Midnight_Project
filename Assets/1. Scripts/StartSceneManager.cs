// using System.Collections;
// using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StartSceneManager : MonoBehaviour
{
    public void OnClickStart()
    {
        SceneManager.LoadScene("2. PlayScene");
    }
}
