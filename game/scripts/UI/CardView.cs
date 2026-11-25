using Godot;

namespace Telo.Game.UI;

/// <summary>
/// The early-access card (DC9): a handwritten card propped against the biscuit tin on the night-shift desk, there once the
/// board is lit. Not an overlay: it is drawn on the card itself and you lean in to read it. The game carries on afterwards.
/// </summary>
public partial class CardView : ModalView
{
    public override void Rebuild()
    {
        Clear(Body); Clear(Footer);
        Title.Text = "EARLY ACCESS ENDS HERE";
        Title.AddThemeFontSizeOverride("font_size", 40);
        Text(Body, "Every light on the board is on. For one night, nothing is broken.", 30);
        Text(Body, "Thank you for playing. You started alone, wiring a frame in a rented room. Tonight the whole planet's traffic runs through a building you poured the slab for.", 24);
        Text(Body, "Coming next, as free updates: Act 6, the phone in everyone's pocket (masts on the hills, and a man in the car park with leaflets). " +
                   "The rack, patched by hand; crossed patches and following the lights when something breaks; the pin board with its string; " +
                   "and a team to hand the building to: Dana, Moss and the night shift.", 22);
        Text(Body, "Your network keeps running and your save carries on. Keep selling, fixing and building: it will all be here when the updates arrive.", 22);
        Text(Body, "P.S. Jory says the big switch was never wired to anything. Don't tell the council.", 20);
        if (IsOpen) CloseButton();
    }
}
