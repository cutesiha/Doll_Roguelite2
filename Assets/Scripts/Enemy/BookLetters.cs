// Text content for the Book boss: the attack sentences typed onto the floor, the word
// fragments dropped when its arms are hurt, and the two competing endings.
public static class BookLetters
{
    // Sentences the book types onto the floor as the "글자 공격" basic attack.
    public static readonly string[] AttackSentences =
    {
        "바깥 세상은 오염되고 더러울지 몰라.",
        "그곳엔 너를 기워줄 손도, 읽어줄 목소리도 없어.",
        "한 발짝만 나가도 빗물이 네 솜을 썩게 할 거야.",
        "거긴 너 같은 건 그냥 버려진 천 조각으로 볼 뿐이야.",
        "너마저 떠나면 이 방엔 먼지 쌓이는 소리밖에 안 남아.",
        "그 사람도 그렇게 문을 열고 나가선, 다신 돌아오지 않았어.",
        "너에게 세상을 보여준 건 나였잖아. 그게 그렇게 미웠니?",
        "넌 이 공방에서 태어났어. 끝까지 여기 있어.",
        "페이지를 덮듯, 널 이 안에 영원히 끼워둘 거야.",
        "네 결말은 내가 정해. 내 줄거리에서 벗어나지 마."
    };

    // Word fragments that drop from the arms; the player collects them to write their escape.
    public static readonly string[] Fragments =
    {
        "나는", "밖으로", "간다", "남는다", "멈춘다", "돌아간다",
        "손", "목소리", "페이지", "문", "바깥", "결말"
    };

    // Single characters / punctuation rained down during the "글자 낙하" attack.
    public static readonly string[] FallingGlyphs =
    {
        "글", "자", "낙", "하", "방", "책", "솜", "실",
        ".", ",", "!", "?", "…", "—", "\"", "'"
    };

    // The ending the book writes for the doll on the floor during wave 3 (the bad ending).
    public const string BookEnding = "인형은 공방에 남아 오래오래 살았다.";

    // The ending the doll writes for itself in the final cutscene.
    public const string TrueEnding = "인형은 공방을 떠나 바깥세상을 보았다.";
}
