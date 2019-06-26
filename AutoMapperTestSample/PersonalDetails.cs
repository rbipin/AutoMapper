
using System.Collections.Generic;

namespace AutoMapperTestSample
{
    public class PersonalDetails
    {
        public string FirstName { get; set; }

        public string LastName { get; set; }

        public ContactDetails Contact { get; set; }

        public ContactDetails ParentDetails { get; set; }

        public PersonalDetails[] Friends { get; set; }

    }

    public class ContactDetails
    {
        public int SomeInt { get; set; }
        public decimal SomeDecimal { get; set; }

        public double SomeDouble { get; set; }

        public string CellNumber { get; set; }

        public string OfficePhoneNumber { get; set; }
    }
}
