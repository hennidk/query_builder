using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace query_builder
{
    [Table("table_2")]
    public class Table2
    {
        [Key]
        [Column("id")]
        public int? Id { get; set; }
        
        [Column("table_1_id")]
        public int? Table1Id { get; set; }
        
        [Column("created_date")]
        public DateTime CreatedDate { get; set; }
        
        [Column("contact_number")]
        public String ContactNumber { get; set; }
        
        [Column("location")]
        public String Location { get; set; }
        
    }
}