using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;



public class CharacterSelectManager : MonoBehaviour
{
    // 오른쪽 큰 캐릭터 이미지
    public Image bigCharacterImage;
    // 캐릭터 이름 텍스트
    public TextMeshProUGUI characterNameText;
    // 캐릭터 설명 텍스트
    public TextMeshProUGUI characterDescText;

    // 각 캐릭터 슬롯 버튼 (Inspector에서 순서대로 연결)
    public Button[] characterButtons;
    // 각 캐릭터의 큰 이미지
    public Sprite[] characterSprites;
    // 각 캐릭터의 이름
    public string[] characterNames;
    // 각 캐릭터의 설명
    public string[] characterDescs;
    
    //Outline 컴포넌트 배열 추가
    public Outline[] Outlines;

    // 현재 선택된 캐릭터 인덱스
    int selectedIndex = 0; 

    void Start()
    {
        // Outline 배열에 각 캐릭터 이미지의 Outline 컴포넌트 넣기
        Outlines = new Outline[characterButtons.Length];
        for (int i = 0; i < characterButtons.Length; i++)
        {
        // 버튼 안에 있는 Image를 찾아서 Outline을 가져옴
        Outlines[i] = characterButtons[i].GetComponentInChildren<Image>().GetComponent<Outline>();
        }
        // 처음엔 첫 번째 캐릭터로 초기화
        SelectCharacter(0);

        // 각 버튼에 클릭 이벤트 연결
        for (int i = 0; i < characterButtons.Length; i++)
        {
            int idx = i; // 지역 변수로 캡처
            characterButtons[i].onClick.AddListener(() => SelectCharacter(idx));
        }
    }

    // 캐릭터 선택 시 호출
    void SelectCharacter(int idx)
    {
        selectedIndex = idx;
        bigCharacterImage.sprite = characterSprites[idx];
        characterNameText.text = characterNames[idx];
        characterDescText.text = characterDescs[idx];
        
        // 모든 Outline을 투명하게 숨기기
        for (int i = 0; i < Outlines.Length; i++)
        {
            if (Outlines[i] != null)
            {
                Outlines[i].effectColor = new Color(0, 0, 0, 0); // 완전 투명
            }
        }

        // 선택된 캐릭터만 파란색 테두리 보이게!
        if (Outlines[idx] != null)
        {
            Outlines[idx].effectColor = Color.black; // 검정색
        }
    }

    // "이 아이로 정했어!" 버튼에 연결할 함수
    public void OnConfirmButtonClick()
    {
        PlayerPrefs.SetInt("SelectedCharacter", selectedIndex);
        SceneManager.LoadScene("3. SetupScene"); // 3번 씬으로 이동
    }
}