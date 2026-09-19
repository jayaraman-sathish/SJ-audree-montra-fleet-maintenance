using MontraFleet.Api.Models;
namespace MontraFleet.Api.Data;
public static class InventoryRules
{
 public static decimal AtTechnician(PartRequest r)=>r.QuantityIssued-r.QuantityReturned-r.QuantityConsumed;
 public static decimal ReservedRemaining(PartRequest r)=>r.QuantityReserved-r.QuantityIssued;
 public static bool Settled(PartRequest r)=>AtTechnician(r)==0&&ReservedRemaining(r)==0&&(r.Status=="Cancelled"||((r.Status=="Consumed"||r.Status=="Returned")&&r.QuantityIssued>=r.QuantityRequired));
 public static decimal Apply(PartStock stock,PartRequest r,string action,decimal requested)
 {
  if(requested<0)throw new InvalidOperationException("Quantity cannot be negative.");
  if(r.Status=="Cancelled")throw new InvalidOperationException("Request is closed.");
  if(r.QuantityRequired<0||ReservedRemaining(r)<0||AtTechnician(r)<0||stock.ReservedQty<ReservedRemaining(r)||stock.OnHandQty<stock.ReservedQty)throw new InvalidOperationException("Stock/request totals are inconsistent. Review reconciliation before posting.");
  decimal maximum=action switch{"reserve"=>r.QuantityRequired-r.QuantityReserved,"issue"=>Math.Min(ReservedRemaining(r),r.QuantityRequired-r.QuantityIssued),"return" or "consume"=>AtTechnician(r),"cancel"=>ReservedRemaining(r),_=>throw new InvalidOperationException("Unknown stock action.")};
  var qty=requested==0?maximum:requested;
  if(action=="cancel"){
   if(AtTechnician(r)>0)throw new InvalidOperationException("Consume or return all issued parts before closing the remaining request.");
   qty=ReservedRemaining(r);stock.ReservedQty-=qty;r.QuantityReserved=r.QuantityIssued;r.Status="Cancelled";
  }else{
   if(qty<=0||qty>maximum)throw new InvalidOperationException("Quantity exceeds the remaining request balance.");
   if(action=="reserve"){if(qty>stock.OnHandQty-stock.ReservedQty)throw new InvalidOperationException("Insufficient available stock.");stock.ReservedQty+=qty;r.QuantityReserved+=qty;}
   if(action=="issue"){if(qty>stock.OnHandQty||qty>stock.ReservedQty)throw new InvalidOperationException("Insufficient reserved stock.");stock.OnHandQty-=qty;stock.ReservedQty-=qty;r.QuantityIssued+=qty;}
   if(action=="return"){stock.OnHandQty+=qty;r.QuantityReturned+=qty;}
   if(action=="consume")r.QuantityConsumed+=qty;
   r.Status=AtTechnician(r)==0&&r.QuantityIssued>=r.QuantityRequired?(r.QuantityConsumed>0?"Consumed":"Returned"):AtTechnician(r)>0?(r.QuantityConsumed>0?"Partially Consumed":r.QuantityReturned>0?"Partially Returned":"Issued"):ReservedRemaining(r)>0?(r.QuantityReserved>=r.QuantityRequired?"Reserved":"Partially Reserved"):"Requested";
  }
  stock.UpdatedAt=r.UpdatedAt=DateTime.UtcNow;return qty;
 }
 public static decimal StockDelta(PartTransaction t)=>t.TransactionType switch{"Receipt" or "Return" or "Stock Count"=>t.Quantity,"Issue"=>-t.Quantity,_=>0};
}
