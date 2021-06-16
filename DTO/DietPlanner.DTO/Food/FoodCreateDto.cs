using System;

using DietPlanner.DTO.Interfaces;

namespace DietPlanner.DTO.Food
{
    public class FoodCreateDto:IDTO
    {
        public string Name { get; set; }
        public string Description { get; set; }


        //------
        public Guid CreateUserId { get; set; }
    }
}
