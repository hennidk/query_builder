using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace query_builder
{
    [Table("table_1")]
    public class Table1
    {
        [Key]
        [Column("id")]
        public int? Id { get; set; }
        
        [Column("created_date")]
        public DateTime CreatedDate { get; set; }
        
        [Column("address")]
        public String Address { get; set; }
        
        [Column("name")]
        public String Name { get; set; }
        
    }
}