// Text content for the Book boss: floor attack sentences, dropped word fragments,
// and the two competing endings used by the final rewrite puzzle.
public static class BookLetters
{
    public static readonly string[] AttackSentences =
    {
        "바깥 세상은 너를 환영하지 않아.",
        "너를 기억해 줄 목소리는 이제 없어.",
        "너는 다시 이 방으로 돌아오게 될 거야.",
        "버려진 조각은 끝내 제자리로 돌아온다.",
        "낡은 책장은 네 결말을 이미 쓰고 있어.",
        "문을 열어도 너는 다시 나를 찾게 될 거야.",
        "네가 본 세상은 모두 찢어진 페이지일 뿐.",
        "공방의 먼지는 네 이름을 덮어 버린다.",
        "이 페이지 위에 영원히 잠들어라.",
        "그 결말은 내가 정한다."
    };

    public static readonly string[] Fragments =
    {
        "공방을", "떠나", "바깥세상을", "보았다", "머물러",
        "돌아가", "꿈꾸었다", "그리워했다", "장인을", "인형들을"
    };

    public static readonly string[] FallingGlyphs =
    {
        "글", "자", "문", "장", "책", "잉", "크", "종",
        ".", ",", "!", "?", "가", "나", "\"", "'"
    };

    public static readonly string[] BookEndingFragments =
    {
        "인형은", "공방에", "남아", "오래오래", "잠들었다."
    };

    public static readonly string[] TrueEndingFragments =
    {
        "인형은", "공방을", "떠나", "바깥세상을", "보았다."
    };

    public const string BookEnding = "인형은 공방에 남아 오래오래 잠들었다.";
    public const string TrueEnding = "인형은 공방을 떠나 바깥세상을 보았다.";
}
