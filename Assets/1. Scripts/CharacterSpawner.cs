using UnityEngine;

public class CharacterSpawner : MonoBehaviour
{
    // 4개의 캐릭터 프리팹 오브젝트(씬에 미리 넣어둔 것)
    public GameObject[] characterPrefabs;

    void Start()
    {
        int selectedIndex = PlayerPrefs.GetInt("SelectedCharacter", 0);

        // 모든 캐릭터 프리팹을 먼저 비활성화
        for (int i = 0; i < characterPrefabs.Length; i++)
        {
            characterPrefabs[i].SetActive(false);
        }

        // 선택된 캐릭터만 활성화
        if (selectedIndex >= 0 && selectedIndex < characterPrefabs.Length)
        {
            characterPrefabs[selectedIndex].SetActive(true);
        }
        else
        {
            Debug.LogError("잘못된 캐릭터 인덱스입니다!");
        }
    }
}
