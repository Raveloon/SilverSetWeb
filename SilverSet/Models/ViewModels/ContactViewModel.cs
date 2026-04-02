using SilverSet.Models.Entities;

namespace SilverSet.Models.ViewModels;
public class ContactViewModel
{
    public Ticket? ActiveTicket { get; set; }
    public int ClosedTicketsCount { get; set; }
}