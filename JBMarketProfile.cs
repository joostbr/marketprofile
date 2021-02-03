#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.Core;
using SharpDX.DirectWrite;
#endregion

//This namespace holds Indicators in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Indicators
{
	public class JBTPOProfile {
		
		public int tradingHours;
		public String dateString;		
		public DateTime sessionStart;
		public String id;
		public Double high = 0;
		public Double low = 1E10;
		public Double poc = 0;
		public Double valHigh = 0;
		public Double valLow = 0;
		public bool highBroken = false;
		public bool lowBroken = false;
		public bool pocBroken = false;
		public bool valLowBroken = false;
		public bool valHighBroken = false;
		
		public int count = 0;
				
		
		// {price --> {100s, 110s, 130s}}  time since start session, the size of the value list will determine the size of the TPO profile at that price
		// add time if time not present (default set behaviour)
		public SortedDictionary<double, HashSet<double>> profile = new SortedDictionary<double, HashSet<double>>(); 
		
		public JBTPOProfile(int tradingHours, DateTime sessionStart) {
			this.tradingHours = tradingHours;
			this.sessionStart = sessionStart;
			this.dateString = sessionStart.ToString("MMM dd");
			this.id = sessionStart.ToString("MMddHHmm")+"_"+tradingHours;
		}
		
		public void add(double price, double time) {
			HashSet<double> set = null;
			if (!profile.TryGetValue(price, out set)) {
				set = new HashSet<double>();
				profile.Add(price, set);				
			}
			int n = set.Count;
			set.Add(time);
			count = count + (set.Count - n);
			if (low>price) {
				low = price;
			}
			if (high<price) {
				high = price;
			}
			poc = 0;
		}
		
		public Double getRangeHigh() {
			return high;
		}
		
		public Double getRangeLow() {
			return low;
		}
		
		public double getValLow() {
			return valLow;
		}
		
		public double getValHigh() {
			return valHigh;
		}
		
		public double getPOC() {
			if (poc == 0) {
				int maxDepth=0;
				double maxDepthPrice = 0;
				double midDistance = 1E10;
				double midPrice = (high+low)/2.0;		
				foreach( double price in profile.Keys )
				{
					double dist = Math.Abs(price-midPrice);
					int count = profile[price].Count;
					if (count>maxDepth || (count==maxDepth && dist<midDistance)) {
						maxDepthPrice = price;
						maxDepth = count;
						midDistance = dist;
					}				    
				}
				poc = maxDepthPrice;
			}
			return poc;
		}
		
		private bool isBetween(double x, double l, double h) {
			return l<x && x<h;
		}
				
		public void checkBrokenLevels(double otherLow, double otherHigh) {
			lowBroken = isBetween(low, otherLow, otherHigh);
			highBroken = isBetween(high, otherLow, otherHigh);
			pocBroken = isBetween(poc, otherLow, otherHigh);
			valLowBroken = isBetween(valLow, otherLow, otherHigh);
			valHighBroken = isBetween(valHigh, otherLow, otherHigh);
		}
		
		public void calculateValueArea(Indicator ind) {
			
			List<double> prices = profile.Keys.ToList();
			int idx = prices.BinarySearch(getPOC());
			double p = prices[idx];
			valHigh = p;
			valLow = p;
			int highIdx=idx+1; 
			int lowIdx =idx-1;	
			int areaThreshold = (int)(0.7 * this.count + 0.5);
			int vol = profile[p].Count;
			while (vol < areaThreshold) {				
				int lowerExt = 0;
				int higherExt = 0;
				int extension = 0;				
				if (highIdx < prices.Count) {
					higherExt = profile[prices[highIdx]].Count;	
					if (highIdx+1 < prices.Count) {
						higherExt = higherExt + profile[prices[highIdx+1]].Count;	
					}
				}
				if (lowIdx >= 0) {
					lowerExt = profile[prices[lowIdx]].Count;
					if (lowIdx-1 >= 0) {
						lowerExt = lowerExt + profile[prices[lowIdx-1]].Count;
					}
				}
				if (higherExt >= lowerExt) {
					extension = higherExt;
					highIdx = highIdx + 2;
				}
				else {
					extension = lowerExt;
					lowIdx = lowIdx - 2;
				}								
				vol = vol + extension;
				//ind.Print("POC "+getPOC()+" PCT "+((double)vol/count)+" low "+lowIdx+" high "+highIdx);
			}			
			valHigh = prices[Math.Min(highIdx,prices.Count-1)];
			valLow = prices[Math.Max(lowIdx,0)];
			
		}
		
	}
	
	public class JBMarketProfile : Indicator
	{
		private TimeZoneInfo centralZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
		private SessionIterator rthSessionIterator;
		private SessionIterator ethSessionIterator;
		private DateTime currentSessionStart;
		
		private JBTPOProfile tpoProfile;
		private double tpoId;
		
		private List<JBTPOProfile> tpoProfiles;
		
		private String renderString = "";
		
		private int activeBarETH = -1;
		private int activeBarRTH = -1;
		
		private static int RTH = 1;
		private static int ETH = 2;
		
		private SimpleFont textFont = new Gui.Tools.SimpleFont("Arial", 8);
		
		private int lineId=1;
		
		protected override void OnStateChange()
		{
			AllowRemovalOfDrawObjects =true;
			
			if (State == State.SetDefaults)
			{
				Description									= @"Enter the description for your new custom Indicator here.";
				Name										= "JBMarketProfile";
				Calculate									= Calculate.OnEachTick;
				IsOverlay									= true;
				DisplayInDataBox							= true;
				DrawOnPricePanel							= true;
				DrawHorizontalGridLines						= true;
				DrawVerticalGridLines						= true;
				PaintPriceMarkers							= true;				
				ScaleJustification							= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				//Disable this property if your indicator requires custom values that cumulate with each new market data event. 
				//See Help Guide for additional information.
				IsSuspendedWhileInactive					= true;
								
				tpoProfiles = new List<JBTPOProfile>();
				
			}
			else if (State == State.Configure)
			{
				Print(BarsArray[0].Instrument.FullName);
				
				//AddDataSeries(BarsArray[0].Instrument.FullName, new BarsPeriod { BarsPeriodType = BarsPeriodType.Minute, Value = 15 }, "CME US Index Futures RTH");               
				AddDataSeries(BarsArray[0].Instrument.FullName, new BarsPeriod { BarsPeriodType = BarsPeriodType.Minute, Value = 30 }, "CME US Index Futures RTH");               
    			AddDataSeries(BarsArray[0].Instrument.FullName, new BarsPeriod { BarsPeriodType = BarsPeriodType.Minute, Value = 30 }, "Overnight Template");               
			
				
				// may need to add a 15min series too, for building the TPO, underlying base series can be whatever like 377 ticks
				
				//TradingHours.String2TradingHours("CME US Index Futures RTH").GetPreviousTradingDayEnd(Time[0]);
				
				Print ("TradingHours sessions "+TradingHours.Sessions.Count);
				for (int i = 0; i < TradingHours.Sessions.Count; i++)
				{
  					Print(String.Format("Session {0}: {1} at {2} to {3} at {4}", i, TradingHours.Sessions[i].BeginDay, TradingHours.Sessions[i].BeginTime,
    				TradingHours.Sessions[i].EndDay, TradingHours.Sessions[i].EndTime));
					Print(TradingHours.TimeZone);
				}
				
				
			}
			else if (State == State.Historical) {
				rthSessionIterator = new SessionIterator(BarsArray[RTH]);		
				ethSessionIterator = new SessionIterator(BarsArray[ETH]);	
				
				SetZOrder(-1);
			}
			else if (State == State.Transition) {
				if (tpoProfile != null) {
					update(tpoProfile);					
				}
				Print("TRANSITION");
			}
		}

		protected override void OnMarketData(MarketDataEventArgs marketDataUpdate)
		{
			DateTime nyTime = TimeZoneInfo.ConvertTime(marketDataUpdate.Time, TimeZoneInfo.Local, centralZone);
				
			//Print(nyTime);
			
		}

		public void printIt(JBTPOProfile tpoProfile) {
			SortedDictionary<double, HashSet<double>>.KeyCollection keyColl = tpoProfile.profile.Keys;

			Print("Session at "+tpoProfile.sessionStart+" varea "+tpoProfile.valLow+" - "+tpoProfile.valHigh+" "+tpoProfile.count);
			foreach( double price in keyColl )
			{
			    Print("Key = {0}, Len {1} "+ price+", "+ tpoProfile.profile[price].Count);
			}
		}
		
		private void checkBrokenLevels(JBTPOProfile profile) {
			foreach (JBTPOProfile each in this.tpoProfiles) {
				each.checkBrokenLevels(profile.getRangeLow(), profile.getRangeHigh());
			}
		}
		
		private void update(JBTPOProfile profile) {
			//printIt(tpoProfile);
			
			tpoProfile.calculateValueArea(this);			
			checkBrokenLevels(tpoProfile);			
			drawTPOLevels(tpoProfile);
	
		}
		
		private SolidColorBrush getBrush(bool broken, SolidColorBrush color) {
			if (broken) {
				SolidColorBrush faded = new SolidColorBrush(Color.FromArgb(150, color.Color.R, color.Color.G, color.Color.B));
                faded.Freeze();
                color = faded;
			}
			return color;
		}
		
		private void drawTPOLevels(JBTPOProfile profile) {
			DrayLabeledRay("HI",  tpoProfile, tpoProfile.getRangeHigh(), getBrush(true,Brushes.Green));
			DrayLabeledRay("LO",  tpoProfile, tpoProfile.getRangeLow() , Brushes.Red);
			DrayLabeledRay("POC", tpoProfile, tpoProfile.getPOC()      , Brushes.Pink);
			DrayLabeledRay("VAH",  tpoProfile, tpoProfile.getValHigh() , Brushes.White);
			DrayLabeledRay("VAL",  tpoProfile, tpoProfile.getValLow()  , Brushes.White);
		}
		
		protected override void OnBarUpdate()
		{
			//Print(Times[BarsInProgress][0]+" "+BarsInProgress);
			if (isStartOfNewSessionRTH()) {		
				//Print("RTH "+Times[RTH][0]);
				
				//Print(Times[BarsInProgress][0]+" "+BarsInProgress);
				//Print(Times[ETH][0]+" session: "+ethSessionIterator.ActualSessionBegin+"  "+CurrentBars[ETH]);
				
				rthSessionIterator.GetNextSession(Times[RTH][0], false);														
					
				if (tpoProfile != null) {					
					update(tpoProfile);
					tpoProfiles.Add(tpoProfile);																
				}
				tpoProfile = new JBTPOProfile(RTH, rthSessionIterator.ActualSessionBegin);
							
			}
			else if (isStartOfNewSessionETH()) {				
				//Print("ETH "+Times[ETH][0]);
				
				//Print(Times[BarsInProgress][0]+" "+BarsInProgress);
				//Print(Times[ETH][0]+" session: "+ethSessionIterator.ActualSessionBegin+"  "+CurrentBars[ETH]);

				ethSessionIterator.GetNextSession(Times[ETH][0], false);						
					
				if (tpoProfile != null) {
					update(tpoProfile);
					tpoProfiles.Add(tpoProfile);										
				}				
				
				tpoProfile = new JBTPOProfile(ETH, ethSessionIterator.ActualSessionBegin);
			}
				
			if (BarsInProgress == RTH && CurrentBars[RTH] != activeBarRTH) {	
				if (tpoProfile != null) {			
					if (State == State.Realtime) {
						update(tpoProfile);
					}
					tpoId = (Times[RTH][0] - tpoProfile.sessionStart).TotalSeconds;
				}
				activeBarRTH = CurrentBars[RTH];
				//Print("RTH TPO ID "+Times[RTH][0]+" "+rthTPOId);
			}
			else if (BarsInProgress == ETH && CurrentBars[ETH] != activeBarETH) {
				
				if (tpoProfile != null) {
					if (State == State.Realtime) {
						update(tpoProfile);
					}
					tpoId = (Times[ETH][0] - tpoProfile.sessionStart).TotalSeconds;
				}
				activeBarETH = CurrentBars[ETH];
				//Print("ETH TPO ID "+Times[ETH][0]+" "+rthTPOId);
			}
					
			
			if (tpoProfile != null) {
				tpoProfile.add(Closes[BarsInProgress][0], tpoId);				
			}
						
						
						
		}
		
		private bool isStartOfNewSessionRTH() {
			return BarsInProgress==RTH && BarsArray[RTH].IsFirstBarOfSession && CurrentBars[RTH]!=activeBarRTH;			
		}
		
		private bool isStartOfNewSessionETH() {
			return BarsInProgress==ETH && BarsArray[ETH].IsFirstBarOfSession && CurrentBars[ETH]!=activeBarETH;			
		}
		
		private LabeledRay DrayLabeledRay(String label, JBTPOProfile profile, double price, Brush color) {
			LabeledRay ray = DrawLL.LabeledRay(this, profile.id+"_"+label, profile.sessionStart, price, Time[0], price, color);
			ray.DisplayText = (profile.tradingHours == ETH ? "ON " : "")+label+" "+profile.dateString;
			ray.Font = textFont;
			ray.AppendPriceTime = false;
			ray.IsLocked =false;
			ray.RoundPrice = true;				
			
			return ray;
		}
		
		/*protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			
			
			base.OnRender(chartControl, chartScale); 
			
			try 
			{
				if(IsInHitTest) return;
							
				SharpDX.DirectWrite.TextFormat textFormat = textFont.ToDirectWriteTextFormat();
				
				foreach (JBTPOProfile profile in this.rthTPOProfiles) {
					
					//Print(profile.getRangeHigh());
					
					SharpDX.Direct2D1.Brush highBrushDx = Brushes.Green.ToDxBrush(RenderTarget);
					SharpDX.Direct2D1.Brush pocBrushDx = Brushes.Pink.ToDxBrush(RenderTarget);
					SharpDX.Direct2D1.Brush lowBrushDx = Brushes.Red.ToDxBrush(RenderTarget);
					SharpDX.Direct2D1.Brush valBrushDx = Brushes.Blue.ToDxBrush(RenderTarget);
					
																		
					drawLabeledLine(chartScale, textFormat, highBrushDx, profile.getRangeHigh(), "HI "+profile.getRangeHigh());
					drawLabeledLine(chartScale, textFormat, lowBrushDx, profile.getRangeLow(), "LO "+profile.getRangeLow());
					drawLabeledLine(chartScale, textFormat, pocBrushDx, profile.poc, "POC"+profile.poc);
					drawLabeledLine(chartScale, textFormat, valBrushDx, profile.valHigh, "VAH"+profile.valHigh);
					drawLabeledLine(chartScale, textFormat, valBrushDx, profile.valLow, "VAL"+profile.valLow);
															
					
					lowBrushDx.Dispose();
					highBrushDx.Dispose();
					valBrushDx.Dispose();
					pocBrushDx.Dispose();
					textFormat.Dispose();
				}

				foreach (JBTPOProfile profile in this.ethTPOProfiles) {
					
					SharpDX.Direct2D1.Brush highBrushDx = Brushes.Green.ToDxBrush(RenderTarget);
					SharpDX.Direct2D1.Brush pocBrushDx = Brushes.Pink.ToDxBrush(RenderTarget);
					SharpDX.Direct2D1.Brush lowBrushDx = Brushes.Red.ToDxBrush(RenderTarget);
					SharpDX.Direct2D1.Brush valBrushDx = Brushes.Blue.ToDxBrush(RenderTarget);
					
																		
					drawLabeledLine(chartScale, textFormat, highBrushDx, profile.getRangeHigh(), ""+profile.getRangeHigh());
					drawLabeledLine(chartScale, textFormat, lowBrushDx, profile.getRangeLow(), ""+profile.getRangeLow());
					drawLabeledLine(chartScale, textFormat, pocBrushDx, profile.poc, ""+profile.poc);
					drawLabeledLine(chartScale, textFormat, valBrushDx, profile.valHigh, ""+profile.valHigh);
					drawLabeledLine(chartScale, textFormat, valBrushDx, profile.valLow, ""+profile.valLow);
					
				
					
					
					lowBrushDx.Dispose();
					highBrushDx.Dispose();
					valBrushDx.Dispose();
					pocBrushDx.Dispose();
					
				}
				
				textFormat.Dispose();
				
			} catch (Exception e) {
				Log("Render market profile error: ", NinjaTrader.Cbi.LogLevel.Warning);
				Print (Time[0]+ " "+ e.ToString()); // send exception to the output
			}
			
		}
		
		private void drawLabeledLine(ChartScale chartScale, TextFormat textFormat, SharpDX.Direct2D1.Brush brushDx, double price, String label) {
			float x = ChartPanel.W - 140 ;
			float y = chartScale.GetYByValue(price);
			SharpDX.Vector2	textPoint = new SharpDX.Vector2(x, y);	
			SharpDX.Vector2 startLine = new SharpDX.Vector2(ChartPanel.X, y);
			SharpDX.Vector2 endLine   = new SharpDX.Vector2(ChartPanel.X+ChartPanel.W, y);		
			
			TextLayout textLayout = new TextLayout(Globals.DirectWriteFactory, label, textFormat, textFormat.FontSize+100, textFormat.FontSize);					
										
			RenderTarget.DrawTextLayout(textPoint, textLayout, brushDx);
			RenderTarget.DrawLine(startLine, endLine, brushDx, 1);
			
			textLayout.Dispose();
		}*/
		
		
	
	}
	
	
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private JBMarketProfile[] cacheJBMarketProfile;
		public JBMarketProfile JBMarketProfile()
		{
			return JBMarketProfile(Input);
		}

		public JBMarketProfile JBMarketProfile(ISeries<double> input)
		{
			if (cacheJBMarketProfile != null)
				for (int idx = 0; idx < cacheJBMarketProfile.Length; idx++)
					if (cacheJBMarketProfile[idx] != null &&  cacheJBMarketProfile[idx].EqualsInput(input))
						return cacheJBMarketProfile[idx];
			return CacheIndicator<JBMarketProfile>(new JBMarketProfile(), input, ref cacheJBMarketProfile);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.JBMarketProfile JBMarketProfile()
		{
			return indicator.JBMarketProfile(Input);
		}

		public Indicators.JBMarketProfile JBMarketProfile(ISeries<double> input )
		{
			return indicator.JBMarketProfile(input);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.JBMarketProfile JBMarketProfile()
		{
			return indicator.JBMarketProfile(Input);
		}

		public Indicators.JBMarketProfile JBMarketProfile(ISeries<double> input )
		{
			return indicator.JBMarketProfile(input);
		}
	}
}

#endregion
