using System;
using System.IO;
using OpenAI;
using OpenAI.Audio;
using OpenAI.Images;

class Test
{
    static void Main()
    {
        var areq = new AudioTranscriptionRequest(audioPath: "test.mp3");
        var ireq = new ImageEditRequest(imagePath: "test.png", maskPath: "test.png", prompt: "test", size: "1024x1024");
    }
}
