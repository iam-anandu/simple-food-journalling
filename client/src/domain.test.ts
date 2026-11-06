import { describe, expect, it } from 'vitest';
import { averageCalories, macroPercentages, seedMeals, sum } from './domain';
describe('nutrition summaries',()=>{
 it('averages only known calorie entries',()=>expect(averageCalories([{...seedMeals[0],calories:100},{...seedMeals[1],calories:null}])).toBe(100));
 it('does not present missing macro data as measured',()=>expect(macroPercentages([{...seedMeals[0],carbs:null}])).toBeNull());
 it('calculates percentages from entered macro energy',()=>expect(macroPercentages([{...seedMeals[0],carbs:50,protein:25,fat:10}])).toEqual({carbs:51,protein:26,fat:23}));
 it('sums entry values',()=>expect(sum(seedMeals,m=>m.portions)).toBe(3));
});
