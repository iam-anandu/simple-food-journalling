export type Category = 'Breakfast' | 'Lunch' | 'Dinner' | 'Snack' | 'Drink';
export interface Meal { id: string; name: string; category: Category; type: 'Meal' | 'Snack' | 'Drink'; eatenAt: string; portions: number; portionLabel: string; calories: number | null; carbs: number | null; protein: number | null; fat: number | null; }
export interface WaterLog { id: string; loggedAt: string; litres: number; }
export interface Profile { displayName: string; email: string; timeZone: string; calorieGoal: number; waterGoal: number; carbsGoal: number; proteinGoal: number; fatGoal: number; reminders: boolean; weeklySummary: boolean; }
export const initialProfile: Profile = { displayName: 'Food lover', email: '', timeZone: 'UTC', calorieGoal: 2000, waterGoal: 2.5, carbsGoal: 45, proteinGoal: 30, fatGoal: 25, reminders: true, weeklySummary: true };
export const seedMeals: Meal[] = [
  { id:'seed-1', name:'Grilled Chicken Salad', category:'Lunch', type:'Meal', eatenAt:'2026-11-05T12:45:00', portions:1, portionLabel:'portion', calories:450, carbs:32, protein:38, fat:18 },
  { id:'seed-2', name:'Oatmeal with Berries', category:'Breakfast', type:'Meal', eatenAt:'2026-11-05T08:30:00', portions:1, portionLabel:'bowl', calories:320, carbs:48, protein:12, fat:8 },
  { id:'seed-3', name:'Banana', category:'Snack', type:'Snack', eatenAt:'2026-11-04T19:15:00', portions:1, portionLabel:'medium', calories:105, carbs:27, protein:1, fat:0 }
];
export function sum<T>(values:T[], pick:(value:T)=>number):number { return values.reduce((total, value)=>total+pick(value),0); }
export function averageCalories(meals:Meal[]):number|null { const known=meals.filter(meal=>meal.calories!==null); const days=new Set(known.map(meal=>meal.eatenAt.slice(0,10))); return days.size ? Math.round(sum(known,meal=>meal.calories!)/days.size) : null; }
export function macroPercentages(meals:Meal[]):{carbs:number;protein:number;fat:number}|null {
  const complete=meals.filter(meal=>meal.carbs!==null&&meal.protein!==null&&meal.fat!==null);
  if (!complete.length) return null;
  const totals={carbs:sum(complete,m=>m.carbs!*m.portions),protein:sum(complete,m=>m.protein!*m.portions),fat:sum(complete,m=>m.fat!*m.portions)};
  const energy=totals.carbs*4+totals.protein*4+totals.fat*9;
  return energy ? {carbs:Math.round(totals.carbs*4/energy*100),protein:Math.round(totals.protein*4/energy*100),fat:Math.round(totals.fat*9/energy*100)} : null;
}
