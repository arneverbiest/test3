using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.IO;



namespace Scheduler
{

    internal enum SessionType
    {
        MorningSession,
        EveningSession
    }



    public class Conference
    {
        //uitgesorteerde lijst van de sprekers op dat moment.
        private List<Track> Tracks { get; set; }


        public void ScheduleTalks(List<Talk> talks)
        {
            //controle op input. dat deze geen null is.
            if (talks.Count() == 0)
            {
                Console.WriteLine("No talks to schedule");
                return;
            }

            try
            {
                //controle of het totaal van de talks niet groter is dan de tijd dat ze hebben.
                double totalDuration = talks.Sum(x => x.Duration);
                //if statement om te controleren of de twee schema's gelijk lopen.
                int numOfTracks = (totalDuration < Track.TotalMinPerTrack) ? 1 : (int)Math.Ceiling(totalDuration / Track.TotalMinPerTrack);
                //nieuwe lijst benoemen
                Tracks = new List<Track>();
                //if statement of counts niet groter is dan 6 anders moet er 1 van tralks verwijderd worden.
                int maxSet = talks.Count() > 6 ? 6 : talks.Count() - 1;
                //toevoegen van elke track
                for (int i = 0; i < numOfTracks; ++i)
                {
                    Tracks.Add(new Track(string.Format("Track {0}", i + 1)));
                    AllocateSessions(talks, i, Track.TotalMinInMorningSession, SessionType.MorningSession, maxSet);
                    AllocateSessions(talks, i, Track.TotalMinInAfterNoonSession, SessionType.EveningSession, maxSet);
                }

                //talks bereknen of zij hierin passen al de overige zouden voor zorgen dat ze buiten de tijd zijn.
                if (talks.Count() > 0)
                {
                    //dus som is gelijk aan de tijd die er mogelijk is.
                    int remainingTalksDuration = talks.Sum(x => x.Duration);
                    for (; maxSet > 0; --maxSet)
                    {
                        for (int index = 0; index < numOfTracks && talks.Count() > 0; ++index)
                        {
                            AllocateSessions(talks, index, Track.TotalMinInMorningSession, SessionType.MorningSession, maxSet);
                            AllocateSessions(talks, index, Track.TotalMinInAfterNoonSession, SessionType.EveningSession, maxSet);
                        }
                    }
                }

                // schrijven naar tekstbestand wat output.txt zal noemen en in momenteel nog in source/repos/test3/bin/debug/net6.0 zal staan
                using (var stream = new StreamWriter(@"output.txt"))
                {
                    CultureInfo culture = CultureInfo.CreateSpecificCulture("en-US");
                    string format = "hh:mm tt";

                    for (int i = 0; i < numOfTracks; ++i)
                    {
                        stream.WriteLine(Tracks[i].Id);
                        DateTime today = DateTime.Today.Add(new TimeSpan(09, 00, 00));

                        foreach (var item in Tracks[i].TalksForSession(SessionType.MorningSession))
                        {
                            stream.WriteLine("{0} {1}", today.ToString(format, CultureInfo.CreateSpecificCulture("en-US")), item);
                            today = today.AddMinutes(item.Duration);
                        }

                        today = Track.LunchTime;

                        stream.WriteLine("{0} Lunch", today.ToString(format, culture));
                        today = today.AddMinutes(Track.MinutesPerHour);

                        foreach (var item in Tracks[i].TalksForSession(SessionType.EveningSession))
                        {
                            stream.WriteLine("{0} {1}", today.ToString(format, culture), item);
                            today = today.AddMinutes(item.Duration);
                        }

                        if (today < Track.FourPM)
                        {
                            today = Track.FourPM;
                        }
                        else if (today > Track.FourPM && today < Track.FivePM)
                        {
                            today = Track.FivePM;
                        }

                        if (today == Track.FourPM || today == Track.FivePM)
                        {
                            stream.WriteLine("{0} Networking Event", today.ToString(format, culture));
                        }
                        else
                        {
                            stream.WriteLine("We went passed the scheduled time for the Networking Event");
                        }
                    }
                }
            }
            //error bericht moest dit niet lukken.
            catch (Exception ex)
            {
                Console.WriteLine("Failed to schedule the talks due to this error.{0}", ex.Message);
            }
        }

        #region helperemethods
        private static IEnumerable<List<Talk>> GetCombinations(int step, int arrayIndex, List<Talk> combination, List<Talk> talks)
        {
            if (step == 0)
            {
                yield return combination;
            }

            for (int i = arrayIndex; i < talks.Count(); ++i)
            {
                combination.Add(talks[i]);
                foreach (var item in GetCombinations(step - 1, i + 1, combination, talks))
                {
                    yield return item;
                }
                combination.RemoveAt(combination.Count() - 1);
            }

        }

        private static List<Talk> LookForSessions(List<Talk> talks, int trackIndex, int totalMinutes, int maxSet)
        {
            List<Talk> combinations = new List<Talk>(talks.Capacity);

            List<Talk> talksInSession = new List<Talk>(maxSet);

            foreach (var item in GetCombinations(maxSet, 0, combinations, talks))
            {
                talksInSession.Clear();
                bool found = false;
                int availableMin = totalMinutes;
                var distinctUnscheduled = item.Where(x => !x.Scheduled).Distinct(new TalkEqualityComparer());

                foreach (var talk in distinctUnscheduled)
                {
                    availableMin -= talk.Duration;
                    talksInSession.Add(talk);
                    if (availableMin == 0)
                    {
                        found = true;
                        break;
                    }
                    if (availableMin < 0)
                    {
                        break;
                    }
                }

                if (found)
                {
                    break;
                }
                else
                {
                    availableMin = totalMinutes;
                }
            }

            return talksInSession;
        }

        private void AllocateSessions(List<Talk> talks, int trackIndex, int totalNumOfMinutes, SessionType sessionType, int maxSet)
        {

            if (Tracks[trackIndex].TalksExistForSession(sessionType))
            {
                return;
            }

            Action<List<Talk>> RemoveScheduledTalks = (t) =>
            {
                for (int i = 0; i < t.Count(); ++i)
                {
                    int index = i;
                    talks.Remove(t[i]);
                }
            };

            var talksForSession = LookForSessions(talks, trackIndex, totalNumOfMinutes, maxSet);
            if (talksForSession.Any())
            {
                Tracks[trackIndex].AddTalksToSession(sessionType, talksForSession);
                RemoveScheduledTalks(talksForSession);
            }
        }

        #endregion

    }



    internal class Session
    {
        //lijst voor de talks
        public List<Talk> Talks { get; set; }
        //maakt dat de files altijd bovenaan komen te staan.
        public bool FilleUp { get; set; }
    }

    internal class Track
    {
        //berekening voor het schema te bepalen.
        private const int SessionStartsAt = 9; // 24 hr format.
        private const int SessionEndsAt = 17;
        private const int LunchHour = 12;
        public static int MinutesPerHour = 60;

        public static int TotalMinPerTrack = (SessionEndsAt - SessionStartsAt - 1) * MinutesPerHour;
        public static int Minutesperhour = 60;
        public static int TotalMinInMorningSession = 60 * (LunchHour - SessionStartsAt);
        public const int TotalMinInAfterNoonSession = 60 * (SessionEndsAt - LunchHour - 1);

        public static DateTime FourPM = DateTime.Today.Add(new TimeSpan(16, 00, 00));
        public static DateTime FivePM = DateTime.Today.Add(new TimeSpan(17, 00, 00));
        public static DateTime LunchTime = DateTime.Today.Add(new TimeSpan(12, 00, 00));

        //id wat nofig is om controle te hebben dat meerdere talks niet op hetzelfde moment worden gedaan.
        public string Id { get; set; }
        private Dictionary<SessionType, Session> Sessions { get; set; }
        public Track(string id)
        {
            Id = id;
            Sessions = new Dictionary<SessionType, Session>();
        }

        //controle gebeurd hier.
        internal bool TalksExistForSession(SessionType sessionType)
        {
            return Sessions.ContainsKey(sessionType) && Sessions[sessionType].FilleUp;
        }

        internal void AddTalksToSession(SessionType sessionType, List<Talk> talksForSession)
        {
            Sessions.Add(sessionType, new Session() { Talks = talksForSession, FilleUp = true });
        }

        internal IEnumerable<Talk> TalksForSession(SessionType sessionType)
        {
            if (Sessions.ContainsKey(sessionType))
            {
                if (Sessions[sessionType].FilleUp)
                {
                    return Sessions[sessionType].Talks;
                }
            }
            return new List<Talk>();
        }
    }
    //klasse van talk
    public class Talk : IEquatable<Talk>
    {
        public string Title { get; set; }
        public int Duration { get; set; } // in minutes.
        public bool Scheduled { get; set; }

        private string DurationFormat
        {
            get
            {
                return Duration == 5 ? "Lightning" : Duration + "min";
            }
        }
        //methode om in de applicatie te kunnen gebruiken zonder telkens alle gegevens te moeten geven.
        public override string ToString()
        {
            return string.Format("{0} {1}", Title, DurationFormat);
        }

        //controle of het object effectief is gebruikt.
        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            var talk = obj as Talk;
            if (talk == null) return false;

            return this.Equals(talk);
        }
        //override die maakt dat wij de naam wel kunnen lezen maar een externe die onze code probeert te lezen niet.
        public override int GetHashCode()
        {
            return Title.GetHashCode();
        }

        public bool Equals(Talk other)
        {
            return this.Title.Equals(other.Title);
        }
    }


    internal class TalkEqualityComparer : IEqualityComparer<Talk>
    {

        public bool Equals(Talk x, Talk y)
        {
            if (Object.ReferenceEquals(x, y)) return true;

            if (Object.ReferenceEquals(x, null)) return false;

            if (Object.ReferenceEquals(y, null)) return false;

            return x.Title.Equals(y.Title);
        }

        public int GetHashCode(Talk obj)
        {
            return obj.Title.GetHashCode();
        }
    }
}




namespace Scheduler
{
    class ConferenceScheduler
    {
        static void Main(string[] args)
        {
            string line;
            var talks = new List<Talk>();

            Func<string, int> GetDuration = (duration) =>
            {
                if (duration.Equals("lightning"))
                {
                    return 5;
                }
                else
                {
                    return Int32.Parse(duration.Substring(0, duration.IndexOf('m')));
                }
            };

            Console.WriteLine("Enter empty line to stop input\n\n");
            while ((line = Console.ReadLine()).Any())
            {
                var tokens = line.Split(new char[] { ' ' });
                var durationTime = tokens.Last();
                var title = string.Join(" ", tokens.Take(tokens.Count() - 1));
                talks.Add(new Talk() { Title = title, Duration = GetDuration(durationTime.ToLower()) });
            }

            Conference conference = new Conference();
            conference.ScheduleTalks(talks);

            Console.WriteLine("Done.Press Enter to Exit");
            Console.ReadKey();

        }
    }
}
