﻿using System.Drawing;

namespace Mappalachia
{
	static class SettingsPlotCluster
	{
		// Cluster mode tuning
		public static readonly float minimumPolygonArea = 250; // The minimum area px^2 that a cluster polygon must fill or else uses a centroid-centered bounding circle
		public static readonly float maximumCircleRadius = 50; // The maximum radius of a would-be circle at which we force a polygon instead of replacing with a circle
		public static readonly int polygonLineThickness = 4;
		public static readonly int boundingCircleMinRadius = 15; // Clusters given a bounding circle are rendered at least this big
		public static readonly int polygonPointReductionRange = 5; // Points in the cluster convex hull this close together are merged
		public static readonly Brush weightBrush = new SolidBrush(Color.FromArgb(200, Color.White)); // Brush used to paint weight text

		public static readonly int minRange = boundingCircleMinRadius * 2;
		public static readonly int maxRange = 800;

		public static readonly bool defaultLiveUpdate = false;
		public static readonly int defaultClusterRange = 100;

		public static bool liveUpdate = defaultLiveUpdate;
		public static int clusterRange = defaultClusterRange;
	}
}
