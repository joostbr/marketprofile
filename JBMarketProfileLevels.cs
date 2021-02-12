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
#endregion

//This namespace holds Indicators in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Indicators
{
	public class JBTPOProfile {
		
		public int tradingHours;
		public String dateString;		
		public DateTime sessionStart;
		public DateTime sessionEnd;
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
		
		public JBTPOProfile(int tradingHours, DateTime sessionStart, DateTime sessionEnd) {
			this.tradingHours = tradingHours;
			this.sessionStart = sessionStart;
			this.sessionEnd = sessionEnd;
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
				
		public bool checkBrokenPOC(double otherLow, double otherHigh) {
			bool newPocBroken = pocBroken | isBetween(poc, otherLow, otherHigh);
			bool result = newPocBroken != pocBroken;
			pocBroken = newPocBroken;
			return result;
		}
		
		public bool checkBrokenLow(double otherLow, double otherHigh) {
			bool newLowBroken = lowBroken | isBetween(low, otherLow, otherHigh);
			bool result = newLowBroken != lowBroken;
			lowBroken = newLowBroken;
			return result;
		}
		
		public bool checkBrokenHigh(double otherLow, double otherHigh) {
			bool newHighBroken = highBroken | isBetween(high, otherLow, otherHigh);
			bool result = newHighBroken != highBroken;
			highBroken = newHighBroken;
			return result;
		}
		
		public bool checkBrokenValLow(double otherLow, double otherHigh) {
			bool newValLowBroken = valLowBroken | isBetween(valLow, otherLow, otherHigh);
			bool result = newValLowBroken != valLowBroken;
			valLowBroken = newValLowBroken;
			return result;
		}
		
		public bool checkBrokenValHigh(double otherLow, double otherHigh) {
			bool newValHighBroken = valHighBroken | isBetween(valHigh, otherLow, otherHigh);
			bool result = newValHighBroken != valHighBroken;
			valHighBroken = newValHighBroken;
			return result;
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
			bool extHigh = true;
			while (vol < areaThreshold) {				
				int lowerExt = 0;
				int higherExt = 0;
				int extension = 0;				
				int stepsHigh = 1;
				int stepsLow = 1;
				if (highIdx < prices.Count) {
					higherExt = profile[prices[highIdx]].Count;	
					if (highIdx+1 < prices.Count && (vol + higherExt) < areaThreshold) {
						higherExt = higherExt + profile[prices[highIdx+1]].Count;	
						stepsHigh++;
					}
				}
				if (lowIdx >= 0) {
					lowerExt = profile[prices[lowIdx]].Count;
					if (lowIdx-1 >= 0 && (vol + lowerExt) < areaThreshold) {
						lowerExt = lowerExt + profile[prices[lowIdx-1]].Count;
						stepsLow++;
					}
				}
				if (higherExt > lowerExt) {
					extension = higherExt;
					highIdx = highIdx + stepsHigh;
					extHigh = true;
				}
				else if (lowerExt > higherExt) {
					extension = lowerExt;
					lowIdx = lowIdx - stepsLow;
					extHigh = true;
				}								
				else { // if equal flip between extending on the higher vs lower side
					if (extHigh) {
						extension = higherExt;
						highIdx = highIdx + stepsHigh;
						extHigh = false;
					}
					else {
						extension = lowerExt;
						lowIdx = lowIdx - stepsLow;
						extHigh = true;
					}
				}
				vol = vol + extension;
				//ind.Print("POC "+getPOC()+" PCT "+((double)vol/count)+" low "+lowIdx+" high "+highIdx);
			}			
			valHigh = prices[Math.Min(highIdx-1,prices.Count-1)];
			valLow = prices[Math.Max(lowIdx+1,0)];
			
		}
		
	}
	
	public class JBMarketProfileLevels : Indicator
	{
		private TimeZoneInfo centralZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
		private SessionIterator rthSessionIterator;
		private SessionIterator ethSessionIterator;
		private DateTime currentSessionStart;
		
		private JBTPOProfile tpoProfile;
		private double tpoId;
		private double settlement;
		
		private List<JBTPOProfile> tpoProfiles;
		
		private String renderString = "";
		
		private int activeBarETH = -1;
		private int activeBarRTH = -1;
		private int activeBar = -1;
		
		private static int RTH = 1;
		private static int ETH = 2;
		
		private int lineId=1;
		
		private bool initDone = false;
		
		protected override void OnStateChange()
		{			
			
			if (State == State.SetDefaults)
			{
				Description									= @"Enter the description for your new custom Indicator here.";
				Name										= "JBMarketProfileLevels";
				Calculate									= Calculate.OnBarClose;
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
								
				LineWidth = 2;
				LineStyle = DashStyleHelper.Dot;
				
				ShowPOC = true;
				ShowONPOC = true;
				ShowVA   = true;
				ShowONVA = true;
				ShowHILO = true;
				ShowONHILO = true;
				
				HILOColor = Brushes.Red;
				VAColor = Brushes.White;
				POCColor = Brushes.Magenta;
				
				ONHILOColor = Brushes.Red;
				ONVAColor = Brushes.White;
				ONPOCColor = Brushes.Magenta;
				
				Opacity = 100;
				BrokenOpacity = 45;
				
				LabelFont = new Gui.Tools.SimpleFont("Arial", 8);
				
				RTHTemplate = "CME US Index Futures RTH";
				ONTemplate = "Overnight Template";
				
				tpoProfiles = new List<JBTPOProfile>();
				
				initDone = false;
				
			}
			else if (State == State.Configure)
			{				
				try {				
					TradingHours.String2TradingHours(RTHTemplate).GetPreviousTradingDayEnd(DateTime.Now); // Test if template exists					
					AddDataSeries(BarsArray[0].Instrument.FullName, new BarsPeriod { BarsPeriodType = BarsPeriodType.Minute, Value = 30 }, RTHTemplate);   					
					try {
						TradingHours.String2TradingHours(ONTemplate).GetPreviousTradingDayEnd(DateTime.Now);  // Test if template exists					
		    			AddDataSeries(BarsArray[0].Instrument.FullName, new BarsPeriod { BarsPeriodType = BarsPeriodType.Minute, Value = 30 }, ONTemplate);     
						initDone = true;
					} catch (Exception e) {
						Draw.TextFixed(this, "NinjaScriptInfo", "JBMarketProfileLevels error loading trading hours with template name '"+ONTemplate+"', check properties", TextPosition.Center);
						Log("JBMarketProfileLevels could not load specified trading hours templates", LogLevel.Error);
					}
				} catch (Exception e) {
					Draw.TextFixed(this, "NinjaScriptInfo", "JBMarketProfileLevels error loading trading hours with template name '"+RTHTemplate+"', check properties", TextPosition.Center);
					Log("JBMarketProfileLevels could not load specified trading hours templates", LogLevel.Error);
				}								
				AllowRemovalOfDrawObjects =true;
				
				//test();
				
			}
			else if (State == State.Historical) {						
				if (!initDone) {
					return;
				}
				if (!Bars.IsTickReplay)
				{
					Draw.TextFixed(this, "NinjaScriptInfo", "JBMarketProfileLevels needs tick replay enabled on the data series when using delta", TextPosition.Center);
					Log("JBMarketProfileLevels needs tick replay enabled on the data series when using delta", LogLevel.Error);
					initDone = false;
				}			
				else {
					rthSessionIterator = new SessionIterator(BarsArray[RTH]);		
					ethSessionIterator = new SessionIterator(BarsArray[ETH]);	
				
					SetZOrder(-1);
				}
			}
			else if (State == State.Transition) {
				if (!initDone) {
					return;
				}
				if (tpoProfile != null) {
					update(tpoProfile);					
				}
				requestAndDrawSettlement(Time[0]);
			}
			else if (State == State.Realtime)
			{				
				if (Instrument != null && Instrument.MarketData != null && Instrument.MarketData.Settlement != null) {
					settlement = Instrument.MarketData.Settlement.Price;			
					requestAndDrawSettlement(DateTime.Now);
				}			
			}
		}


		public void printIt(JBTPOProfile tpoProfile) {
			SortedDictionary<double, HashSet<double>>.KeyCollection keyColl = tpoProfile.profile.Keys;

			Print("Session at "+tpoProfile.sessionStart+" varea "+tpoProfile.valLow+" - "+tpoProfile.valHigh+" "+tpoProfile.count);
			foreach( double price in keyColl )
			{
			    Print("TPO "+ price+", "+ tpoProfile.profile[price].Count);
			}
		}
		
		private void checkBrokenLevels(JBTPOProfile profile) {
			foreach (JBTPOProfile each in this.tpoProfiles) {
				if (each != profile) {
					if (each.tradingHours == RTH) {
						if (ShowPOC && each.checkBrokenPOC(profile.getRangeLow(), profile.getRangeHigh())) {
							DrawLabeledRay("POC", each, each.getPOC(), getBrush(true, POCColor));
						}
						if (ShowHILO && each.checkBrokenHigh(profile.getRangeLow(), profile.getRangeHigh())) {
							DrawLabeledRay("HI", each, each.getRangeHigh(), getBrush(true,HILOColor));
						}					
						if (ShowHILO && each.checkBrokenLow(profile.getRangeLow(), profile.getRangeHigh())) {
							DrawLabeledRay("LO", each, each.getRangeLow(), getBrush(true, HILOColor));
						}
						if (ShowVA && each.checkBrokenValHigh(profile.getRangeLow(), profile.getRangeHigh())) {
							DrawLabeledRay("VAH", each, each.getValHigh(), getBrush(true, VAColor));
						}
						if (ShowVA && each.checkBrokenValLow(profile.getRangeLow(), profile.getRangeHigh())) {
							DrawLabeledRay("VAL", each, each.getValLow(), getBrush(true, VAColor));
						}
					}
					else {
						if (ShowONPOC && each.checkBrokenPOC(profile.getRangeLow(), profile.getRangeHigh())) {
							DrawLabeledRay("ON POC", each, each.getPOC(), getBrush(true, ONPOCColor));
						}
						if (ShowONHILO && each.checkBrokenHigh(profile.getRangeLow(), profile.getRangeHigh())) {
							DrawLabeledRay("ON HI", each, each.getRangeHigh(), getBrush(true, ONHILOColor));
						}					
						if (ShowONHILO && each.checkBrokenLow(profile.getRangeLow(), profile.getRangeHigh())) {
							DrawLabeledRay("ON LO", each, each.getRangeLow(), getBrush(true, ONHILOColor));
						}
						if (ShowONVA && each.checkBrokenValHigh(profile.getRangeLow(), profile.getRangeHigh())) {
							DrawLabeledRay("ON VAH", each, each.getValHigh(), getBrush(true, ONVAColor));
						}
						if (ShowONVA && each.checkBrokenValLow(profile.getRangeLow(), profile.getRangeHigh())) {
							DrawLabeledRay("ON VAL", each, each.getValLow(), getBrush(true, ONVAColor));
						}
					}
				}
			}
		}
		
		private void update(JBTPOProfile profile) {
			//printIt(tpoProfile);
			
			if (profile.count > 10) {
				profile.calculateValueArea(this);			
				checkBrokenLevels(profile);			
				drawTPOLevels(profile);
			}
	
		}
		
		private SolidColorBrush getBrush(bool broken, SolidColorBrush color) {
			if (broken) {
				SolidColorBrush faded = new SolidColorBrush(Color.FromArgb((byte)((BrokenOpacity / 100.0)*255), color.Color.R, color.Color.G, color.Color.B));
                faded.Freeze();
                color = faded;
			}
			else if (Opacity != 100) {
				SolidColorBrush faded = new SolidColorBrush(Color.FromArgb((byte)((Opacity / 100.0)*255), color.Color.R, color.Color.G, color.Color.B));
                faded.Freeze();
                color = faded;
			}
			return color;
		}
		
		private void drawTPOLevels(JBTPOProfile profile) {
			if (profile.tradingHours == RTH) {
				if (ShowHILO) {
					DrawLabeledRay("HI",  tpoProfile, tpoProfile.getRangeHigh(), getBrush(profile.highBroken, HILOColor));
					DrawLabeledRay("LO",  tpoProfile, tpoProfile.getRangeLow() , getBrush(profile.lowBroken, HILOColor));
				}
				if (ShowPOC) {
					DrawLabeledRay("POC", tpoProfile, tpoProfile.getPOC()      , getBrush(profile.pocBroken, POCColor));
				}
				if (ShowVA) {
					DrawLabeledRay("VAH",  tpoProfile, tpoProfile.getValHigh() , getBrush(profile.valHighBroken, VAColor));
					DrawLabeledRay("VAL",  tpoProfile, tpoProfile.getValLow()  , getBrush(profile.valLowBroken, VAColor));
				}
			}
			else {
				if (ShowONHILO) {
					DrawLabeledRay("ON HI",  tpoProfile, tpoProfile.getRangeHigh(), getBrush(profile.highBroken, ONHILOColor));
					DrawLabeledRay("ON LO",  tpoProfile, tpoProfile.getRangeLow() , getBrush(profile.lowBroken, ONHILOColor));
				}
				if (ShowONPOC) {
					DrawLabeledRay("ON POC", tpoProfile, tpoProfile.getPOC()      , getBrush(profile.pocBroken, ONPOCColor));
				}
				if (ShowONVA) {
					DrawLabeledRay("ON VAH",  tpoProfile, tpoProfile.getValHigh() , getBrush(profile.valHighBroken, ONVAColor));
					DrawLabeledRay("ON VAL",  tpoProfile, tpoProfile.getValLow()  , getBrush(profile.valLowBroken, ONVAColor));
				}
			}
		}
		
		protected override void OnMarketData(MarketDataEventArgs e)
		{		
			if (!initDone) {
				return;
			}
			if (e.MarketDataType == MarketDataType.Settlement) {				
				settlement = e.Price;
				return;
			}
			
			if(e.MarketDataType == MarketDataType.Last) {				
				if (isStartOfNewSessionRTH()) {
					Print(e.Time+" "+BarsInProgress+" "+BarsArray[RTH].IsFirstBarOfSession);
					if (tpoProfile != null) {					
						update(tpoProfile);
						tpoProfiles.Add(tpoProfile);																
					}
					rthSessionIterator.GetNextSession(e.Time, false);
					tpoProfile = new JBTPOProfile(RTH, rthSessionIterator.ActualSessionBegin, rthSessionIterator.ActualSessionEnd);
					Print("SESSION - "+tpoProfile.sessionStart+" - "+tpoProfile.sessionEnd);
				}
				if (isStartOfNewSessionETH()) {
					Print(e.Time+" "+BarsInProgress+" "+BarsArray[ETH].IsFirstBarOfSession);
					if (tpoProfile != null) {					
						update(tpoProfile);
						tpoProfiles.Add(tpoProfile);																
					}
					ethSessionIterator.GetNextSession(e.Time, false);
					tpoProfile = new JBTPOProfile(ETH, ethSessionIterator.ActualSessionBegin, ethSessionIterator.ActualSessionEnd);
					Print("SESSION - "+tpoProfile.sessionStart+" - "+tpoProfile.sessionEnd);
					
				}
				
				if (BarsInProgress == RTH && CurrentBars[RTH] != activeBarRTH) {	
					tpoId = (Times[RTH][0] - tpoProfile.sessionStart).TotalSeconds;
					activeBarRTH = CurrentBars[RTH];
				}
				else if (BarsInProgress == ETH && CurrentBars[ETH] != activeBarETH) {								
					tpoId = (Times[ETH][0] - tpoProfile.sessionStart).TotalSeconds;				
					activeBarETH = CurrentBars[ETH];
				}
				if ((BarsInProgress == RTH || BarsInProgress == ETH) && tpoProfile != null) {
					if (e.Time>=tpoProfile.sessionStart && e.Time<tpoProfile.sessionEnd) {
						tpoProfile.add(e.Price, tpoId);
					}
				}
				else if (BarsInProgress == 0 && CurrentBars[0] != activeBar) {
					if (tpoProfile != null) {
						update(tpoProfile);
					}
					activeBar = CurrentBars[0];
				}
			}
		}
		
		protected override void OnBarUpdate()
		{				
			
		}
		
		private bool isStartOfNewSessionRTH() {
			return BarsInProgress==RTH && BarsArray[RTH].IsFirstBarOfSession && CurrentBars[RTH]!=activeBarRTH;			
		}
		
		private bool isStartOfNewSessionETH() {
			return BarsInProgress==ETH && BarsArray[ETH].IsFirstBarOfSession && CurrentBars[ETH]!=activeBarETH;			
		}
		
		private LabeledRay DrawLabeledRay(String label, JBTPOProfile profile, double price, Brush color) {
			RemoveDrawObject(profile.id+"_"+label);

			LabeledRay ray = DrawLL.LabeledRay(this,  profile.id+"_"+label, profile.sessionStart, price, Time[0], price, color, LineStyle, LineWidth);
			
			ray.DisplayText = label+" "+profile.dateString;
			ray.Font = LabelFont;
			ray.AppendPriceTime = false;
			ray.IsLocked =false;
			ray.RoundPrice = true;				
			
			return ray;
		}
		
		private LabeledRay DrawSettlementRay(String label, DateTime settleTime, double price, Brush color) {
			RemoveDrawObject(label);

			LabeledRay ray = DrawLL.LabeledRay(this,  label, settleTime, price, DateTime.Now, price, color, LineStyle, LineWidth);
			
			ray.DisplayText = label;
			ray.Font = LabelFont;
			ray.AppendPriceTime = false;
			ray.IsLocked =false;
			ray.RoundPrice = true;				
			
			return ray;
		}
		
		private void requestAndDrawSettlement(DateTime time) {
		
			
		  DateTime previousEnd = TradingHours.String2TradingHours(RTHTemplate).GetPreviousTradingDayEnd(time);
		
		  BarsRequest barsRequest = new BarsRequest(BarsArray[0].Instrument, previousEnd.AddHours(-1), previousEnd);
		 
		
		  barsRequest.BarsPeriod = new BarsPeriod { BarsPeriodType = BarsPeriodType.Day, Value = 1 };
		
		  barsRequest.Request(new Action<BarsRequest, ErrorCode, string>((bars, errorCode, errorMessage) =>
		  {
		    if (errorCode != ErrorCode.NoError)
		    {
		
		      Print(string.Format("Error on requesting bars: {0}, {1}", errorCode, errorMessage));
		      return;
		    }
		 
		    if (bars.Bars.Count > 0) {
				this.settlement = bars.Bars.GetClose(bars.Bars.Count-1);
				DrawSettlementRay("SETTLEMENT", previousEnd, settlement ,Brushes.Yellow);
			}		    		   
		   
		  }));
		}
		
	
		#region properties
		
		[NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "Level Opacity(%)", Description = "Opacity percentage for level lines", Order = 1, GroupName = "Parameters")]
        public int Opacity
        { get; set; }
		
		
		[NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "Broken Level Opacity(%)", Description = "Opacity percentage for broken level lines", Order = 2, GroupName = "Parameters")]
        public int BrokenOpacity
        { get; set; }
		
		[NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Show POC", Description = "Show Point of Control", Order = 3, GroupName = "Parameters")]
        public bool ShowPOC
        { get; set; }

 		[NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "POC Color", Description = "Line color for Point of Control", Order = 4, GroupName = "Parameters")]
        public SolidColorBrush POCColor
        { get; set; }

        [Browsable(false)]
        public string POCColorSerializable
        {
            get { return Serialize.BrushToString(POCColor); }
            set { POCColor = (SolidColorBrush)Serialize.StringToBrush(value); }
        }
		
		[NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Show HI/LO", Description = "Show Highs and Lows", Order = 5, GroupName = "Parameters")]
        public bool ShowHILO
        { get; set; }

		[NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "HI/LO Color", Description = "Line color for High & Low of session", Order = 6, GroupName = "Parameters")]
        public SolidColorBrush HILOColor
        { get; set; }

        [Browsable(false)]
        public string HILOColorSerializable
        {
            get { return Serialize.BrushToString(HILOColor); }
            set { HILOColor = (SolidColorBrush)Serialize.StringToBrush(value); }
        }
		
		[NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Show VAH/VAL", Description = "Show Value Area Low & High", Order = 7, GroupName = "Parameters")]
        public bool ShowVA
        { get; set; }

		[NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "VAH/VAL Color", Description = "Line color for Value Area High & Low", Order = 8, GroupName = "Parameters")]
        public SolidColorBrush VAColor
        { get; set; }

        [Browsable(false)]
        public string VAColorSerializable
        {
            get { return Serialize.BrushToString(VAColor); }
            set { VAColor = (SolidColorBrush)Serialize.StringToBrush(value); }
        }
		
		[NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Show ON POC", Description = "Show overnight POC", Order = 9, GroupName = "Parameters")]
        public bool ShowONPOC
        { get; set; }
		
		[NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "ON POC Color", Description = "Line color for overnight Point of Control", Order = 10, GroupName = "Parameters")]
        public SolidColorBrush ONPOCColor
        { get; set; }

        [Browsable(false)]
        public string ONPOCColorSerializable
        {
            get { return Serialize.BrushToString(ONPOCColor); }
            set { ONPOCColor = (SolidColorBrush)Serialize.StringToBrush(value); }
        }
				
		[NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Show ON HI/LO", Description = "Show overnight High & Low", Order = 11, GroupName = "Parameters")]
        public bool ShowONHILO
        { get; set; }
		
		[NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "ON HI/LO Color", Description = "Line color for High & Low of overnight session", Order = 12, GroupName = "Parameters")]
        public SolidColorBrush ONHILOColor
        { get; set; }

        [Browsable(false)]
        public string ONHILOColorSerializable
        {
            get { return Serialize.BrushToString(ONHILOColor); }
            set { ONHILOColor = (SolidColorBrush)Serialize.StringToBrush(value); }
        }
		
		[NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Show ON VAH/VAL", Description = "Show overnight Value Area Low and High", Order = 13, GroupName = "Parameters")]
        public bool ShowONVA
        { get; set; }
		
		[NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "ON VAH/VAL Color", Description = "Line color for overnight Value Area High & Low", Order = 14, GroupName = "Parameters")]
        public SolidColorBrush ONVAColor
        { get; set; }

        [Browsable(false)]
        public string ONVAColorSerializable
        {
            get { return Serialize.BrushToString(ONVAColor); }
            set { ONVAColor = (SolidColorBrush)Serialize.StringToBrush(value); }
        }
		
		[NinjaScriptProperty]
		[Display(Name = "Label Font", Description = "Level label font", Order = 15, GroupName = "Parameters")]
		public NinjaTrader.Gui.Tools.SimpleFont LabelFont
		{
			get; set;
		}
		
		[Range(1, int.MaxValue)]
        [NinjaScriptProperty]
        [Display(Name = "Line width", Description = "Line width for levels", Order = 16, GroupName = "Parameters")]
        public int LineWidth
        {
            get; set;
        }

        [NinjaScriptProperty]
        [Display(Name = "Line style", Description = "Line style for levels", Order = 17, GroupName = "Parameters")]
        public DashStyleHelper LineStyle
        {
            get; set;
        }
		
		[NinjaScriptProperty]
        [Display(Name = "RTH Trading Hours", Description = "Regular trading hours template (cfr Tools/TradingHours)", Order = 18, GroupName = "Parameters")]
        public String RTHTemplate
        {
            get; set;
        }
		
		[NinjaScriptProperty]
        [Display(Name = "ON Trading Hours", Description = "Overnight trading hours template (cfr Tools/TradingHours)", Order = 19, GroupName = "Parameters")]
        public String ONTemplate
        {
            get; set;
        }
		
		#endregion
		
	}

	
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private JBMarketProfileLevels[] cacheJBMarketProfileLevels;
		public JBMarketProfileLevels JBMarketProfileLevels(int opacity, int brokenOpacity, bool showPOC, SolidColorBrush pOCColor, bool showHILO, SolidColorBrush hILOColor, bool showVA, SolidColorBrush vAColor, bool showONPOC, SolidColorBrush oNPOCColor, bool showONHILO, SolidColorBrush oNHILOColor, bool showONVA, SolidColorBrush oNVAColor, NinjaTrader.Gui.Tools.SimpleFont labelFont, int lineWidth, DashStyleHelper lineStyle, String rTHTemplate, String oNTemplate)
		{
			return JBMarketProfileLevels(Input, opacity, brokenOpacity, showPOC, pOCColor, showHILO, hILOColor, showVA, vAColor, showONPOC, oNPOCColor, showONHILO, oNHILOColor, showONVA, oNVAColor, labelFont, lineWidth, lineStyle, rTHTemplate, oNTemplate);
		}

		public JBMarketProfileLevels JBMarketProfileLevels(ISeries<double> input, int opacity, int brokenOpacity, bool showPOC, SolidColorBrush pOCColor, bool showHILO, SolidColorBrush hILOColor, bool showVA, SolidColorBrush vAColor, bool showONPOC, SolidColorBrush oNPOCColor, bool showONHILO, SolidColorBrush oNHILOColor, bool showONVA, SolidColorBrush oNVAColor, NinjaTrader.Gui.Tools.SimpleFont labelFont, int lineWidth, DashStyleHelper lineStyle, String rTHTemplate, String oNTemplate)
		{
			if (cacheJBMarketProfileLevels != null)
				for (int idx = 0; idx < cacheJBMarketProfileLevels.Length; idx++)
					if (cacheJBMarketProfileLevels[idx] != null && cacheJBMarketProfileLevels[idx].Opacity == opacity && cacheJBMarketProfileLevels[idx].BrokenOpacity == brokenOpacity && cacheJBMarketProfileLevels[idx].ShowPOC == showPOC && cacheJBMarketProfileLevels[idx].POCColor == pOCColor && cacheJBMarketProfileLevels[idx].ShowHILO == showHILO && cacheJBMarketProfileLevels[idx].HILOColor == hILOColor && cacheJBMarketProfileLevels[idx].ShowVA == showVA && cacheJBMarketProfileLevels[idx].VAColor == vAColor && cacheJBMarketProfileLevels[idx].ShowONPOC == showONPOC && cacheJBMarketProfileLevels[idx].ONPOCColor == oNPOCColor && cacheJBMarketProfileLevels[idx].ShowONHILO == showONHILO && cacheJBMarketProfileLevels[idx].ONHILOColor == oNHILOColor && cacheJBMarketProfileLevels[idx].ShowONVA == showONVA && cacheJBMarketProfileLevels[idx].ONVAColor == oNVAColor && cacheJBMarketProfileLevels[idx].LabelFont == labelFont && cacheJBMarketProfileLevels[idx].LineWidth == lineWidth && cacheJBMarketProfileLevels[idx].LineStyle == lineStyle && cacheJBMarketProfileLevels[idx].RTHTemplate == rTHTemplate && cacheJBMarketProfileLevels[idx].ONTemplate == oNTemplate && cacheJBMarketProfileLevels[idx].EqualsInput(input))
						return cacheJBMarketProfileLevels[idx];
			return CacheIndicator<JBMarketProfileLevels>(new JBMarketProfileLevels(){ Opacity = opacity, BrokenOpacity = brokenOpacity, ShowPOC = showPOC, POCColor = pOCColor, ShowHILO = showHILO, HILOColor = hILOColor, ShowVA = showVA, VAColor = vAColor, ShowONPOC = showONPOC, ONPOCColor = oNPOCColor, ShowONHILO = showONHILO, ONHILOColor = oNHILOColor, ShowONVA = showONVA, ONVAColor = oNVAColor, LabelFont = labelFont, LineWidth = lineWidth, LineStyle = lineStyle, RTHTemplate = rTHTemplate, ONTemplate = oNTemplate }, input, ref cacheJBMarketProfileLevels);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.JBMarketProfileLevels JBMarketProfileLevels(int opacity, int brokenOpacity, bool showPOC, SolidColorBrush pOCColor, bool showHILO, SolidColorBrush hILOColor, bool showVA, SolidColorBrush vAColor, bool showONPOC, SolidColorBrush oNPOCColor, bool showONHILO, SolidColorBrush oNHILOColor, bool showONVA, SolidColorBrush oNVAColor, NinjaTrader.Gui.Tools.SimpleFont labelFont, int lineWidth, DashStyleHelper lineStyle, String rTHTemplate, String oNTemplate)
		{
			return indicator.JBMarketProfileLevels(Input, opacity, brokenOpacity, showPOC, pOCColor, showHILO, hILOColor, showVA, vAColor, showONPOC, oNPOCColor, showONHILO, oNHILOColor, showONVA, oNVAColor, labelFont, lineWidth, lineStyle, rTHTemplate, oNTemplate);
		}

		public Indicators.JBMarketProfileLevels JBMarketProfileLevels(ISeries<double> input , int opacity, int brokenOpacity, bool showPOC, SolidColorBrush pOCColor, bool showHILO, SolidColorBrush hILOColor, bool showVA, SolidColorBrush vAColor, bool showONPOC, SolidColorBrush oNPOCColor, bool showONHILO, SolidColorBrush oNHILOColor, bool showONVA, SolidColorBrush oNVAColor, NinjaTrader.Gui.Tools.SimpleFont labelFont, int lineWidth, DashStyleHelper lineStyle, String rTHTemplate, String oNTemplate)
		{
			return indicator.JBMarketProfileLevels(input, opacity, brokenOpacity, showPOC, pOCColor, showHILO, hILOColor, showVA, vAColor, showONPOC, oNPOCColor, showONHILO, oNHILOColor, showONVA, oNVAColor, labelFont, lineWidth, lineStyle, rTHTemplate, oNTemplate);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.JBMarketProfileLevels JBMarketProfileLevels(int opacity, int brokenOpacity, bool showPOC, SolidColorBrush pOCColor, bool showHILO, SolidColorBrush hILOColor, bool showVA, SolidColorBrush vAColor, bool showONPOC, SolidColorBrush oNPOCColor, bool showONHILO, SolidColorBrush oNHILOColor, bool showONVA, SolidColorBrush oNVAColor, NinjaTrader.Gui.Tools.SimpleFont labelFont, int lineWidth, DashStyleHelper lineStyle, String rTHTemplate, String oNTemplate)
		{
			return indicator.JBMarketProfileLevels(Input, opacity, brokenOpacity, showPOC, pOCColor, showHILO, hILOColor, showVA, vAColor, showONPOC, oNPOCColor, showONHILO, oNHILOColor, showONVA, oNVAColor, labelFont, lineWidth, lineStyle, rTHTemplate, oNTemplate);
		}

		public Indicators.JBMarketProfileLevels JBMarketProfileLevels(ISeries<double> input , int opacity, int brokenOpacity, bool showPOC, SolidColorBrush pOCColor, bool showHILO, SolidColorBrush hILOColor, bool showVA, SolidColorBrush vAColor, bool showONPOC, SolidColorBrush oNPOCColor, bool showONHILO, SolidColorBrush oNHILOColor, bool showONVA, SolidColorBrush oNVAColor, NinjaTrader.Gui.Tools.SimpleFont labelFont, int lineWidth, DashStyleHelper lineStyle, String rTHTemplate, String oNTemplate)
		{
			return indicator.JBMarketProfileLevels(input, opacity, brokenOpacity, showPOC, pOCColor, showHILO, hILOColor, showVA, vAColor, showONPOC, oNPOCColor, showONHILO, oNHILOColor, showONVA, oNVAColor, labelFont, lineWidth, lineStyle, rTHTemplate, oNTemplate);
		}
	}
}

#endregion
