namespace SilverSet.Models.ViewModels;
public class ContactViewModel
{
    public SilverSet.Models.Entities.Ticket? ActiveTicket { get; set; }
    public int ClosedTicketsCount { get; set; }
}