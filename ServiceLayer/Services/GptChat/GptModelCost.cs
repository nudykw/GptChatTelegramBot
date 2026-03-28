namespace ServiceLayer.Services.GptChat
{
    internal record GptModelCost(int PerTokens, decimal Input, decimal Output);
}
