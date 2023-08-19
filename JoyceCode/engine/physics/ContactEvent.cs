namespace engine.physics;

public class ContactEvent : engine.news.Event
{
    public static string PHYSICS_CONTACT_INFO = "physics.contactInfo";
    public ContactInfo ContactInfo;
    public ContactEvent(ContactInfo contactInfo) 
        : base(PHYSICS_CONTACT_INFO, "")
    {
        ContactInfo = contactInfo;
    }
}