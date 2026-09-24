namespace Microsoft.VisualStudio.Text.Editor
{
	internal interface IMdTextView : ITextView2
    {
        new MonoDevelop.SourceEditor.IMDSpaceReservationManager GetSpaceReservationManager(string name);

        Gtk.Container VisualElement
        {
            get;
        }

        void Focus();
	}
}