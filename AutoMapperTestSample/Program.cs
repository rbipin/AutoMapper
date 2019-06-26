using System;
using System.Collections.Generic;
using AutoMapper;

namespace AutoMapperTestSample
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            PersonalDetails mydetails = new PersonalDetails()
            {
                FirstName="Bipin",
                LastName="Radhakrishnan"
            };
            //mydetails.Contact = new ContactDetails()
            //{
            //   CellNumber="616 256 1788",
            //};

            ContactDetails myOfficeContact = new ContactDetails();
            myOfficeContact.OfficePhoneNumber = "247 897 1478";

            

            PersonalDetails[] newFriends = new PersonalDetails[2]
            {
                new PersonalDetails() { FirstName = "abcd" },
                new PersonalDetails() { FirstName = "efgh" }
            };
            //mydetails.Friends = new PersonalDetails[2]
            //{
            //    new PersonalDetails() { FirstName="1234"},
            //    new PersonalDetails() { FirstName = "3456" }
            //};
            

            mydetails.Map(myOfficeContact);
            Console.Read();
        }
    }
}
