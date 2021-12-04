using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace query_builder
{
    public class ReturnClass
    {
        [Column("id")]
        public int? Id { get; set; }
        
        [Column("created_date")]
        [QueryTable(typeof(Table1))] //select "created_date" from table_1, not table_2
        public DateTime CreatedDate { get; set; }
        
        [Column("address")]
        public String Address { get; set; }
        
        [Column("contact_number")]
        public String ContactNumber { get; set; }
        
        [Column("location")]
        public String Location { get; set; }
    }
}