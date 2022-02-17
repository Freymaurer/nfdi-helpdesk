module Captcha

open SixLabors.ImageSharp
open SixLabors.ImageSharp.Processing;
open SixLabors.ImageSharp.PixelFormats
open SixLabors.ImageSharp.Drawing.Processing;
open SixLabors
open SixLabors.Fonts
open System
open System.IO

let private fontArr = [|"Arial"; "Verdana"; "Times New Roman"|]
let private colorArr = Color.WebSafePalette.ToArray()
let private charArr = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ01234567890"
let private rand = System.Random()
let private backcolor = Color.AliceBlue
let private textColor = Color.Black
let private fontsize = float32 40
let private rotationDegree = 30

let createCaptchaString(length:int) =
    String.init length (fun i -> string charArr.[rand.Next(0,charArr.Length-1)])

let createCaptchaImgBase64(captchaClear:string) =
    /// split captcha up a bit to spread it more evenly and avoid overlaps
    let captcha = captchaClear |> Seq.map string |> String.concat " "
    let font = Fonts.SystemFonts.CreateFont(fontArr.[rand.Next(0,fontArr.Length-1)], fontsize, Fonts.FontStyle.Regular)
    let stringHeight, stringWidth = 
        let m = TextMeasurer.Measure(captcha, TextOptions(font))
        m.Height, m.Width
    let imgHeight, imgWidth = stringHeight * float32 1.5, stringWidth * float32 2
    /// calculate effective horizontal start
    let startX = 
        let halfStringWidth = stringWidth / float32 2
        let halfWidth = float imgWidth/2.
        float32 halfWidth - halfStringWidth
    /// calculate vertical center
    let startY = 
        let halfStringHeight = stringHeight / float32 2
        let halfHeight = float imgHeight/2.
        float32 halfHeight - halfStringHeight
    /// create img object
    let img = new Image<Rgba32>(int imgWidth,int imgHeight)
    /// set background color
    img.Mutate(
            fun x -> x.BackgroundColor(backcolor) |> ignore
        )
    let mutable position = startX

    /// create a new image object for each character in captcha. Can manipulate each letter then add it to img
    for c in captcha do
        let imgCharacter = new Image<Rgba32>(int imgWidth, int imgHeight)
        let font = Fonts.SystemFonts.CreateFont(fontArr.[rand.Next(0,fontArr.Length-1)], fontsize, Fonts.FontStyle.Regular)
        let location = new PointF(position, startY);
        position <- position + TextMeasurer.Measure(c.ToString(), TextOptions(font)).Width
        let color = textColor //colors.[rand.Next(0,colors.Length-1)]
        imgCharacter.Mutate(
            fun x -> x.DrawText(c.ToString(), font, color, location) |> ignore
        )

        let builder = new AffineTransformBuilder()        

        imgCharacter.Mutate(fun x ->
            x.Transform(
                let rndRotationDegree = rand.Next(-rotationDegree,rotationDegree) |> float32
                let rotationLocation = System.Numerics.Vector2(position, startY)// new PointF(position,startY)
                builder.PrependRotationDegrees(rndRotationDegree, rotationLocation) 
            ) |> ignore
        )

        img.Mutate(fun x -> x.DrawImage(imgCharacter, float32 1) |> ignore ) 
        imgCharacter.Dispose()

    let stream = new MemoryStream();
    img.Save(stream,Formats.Png.PngEncoder());
    img.Dispose()
    stream.ToArray() |> Convert.ToBase64String