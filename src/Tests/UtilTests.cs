using JoySetlistBotConsole;
using Xunit;

namespace JoySetlistBotTests
{
    public class UtilTests
    {
        [Fact]
        public void ParseQuery_ArtistCity_ValidSetlist()
        {
            // arrange
            var query = "foo fighters moscow";
            
            // act
            var actualResult = Util.ParseQuery(query);
            
            // assert
            Assert.Equal("foo fighters", actualResult.Artist.Name);
            Assert.Equal("moscow", actualResult.Venue.City.Name);
        }
        
        [Fact]
        public void ParseQuery_ArtistYear_ValidSetlist()
        {
            // arrange
            var query = "foo fighters 2019";
            
            // act
            var actualResult = Util.ParseQuery(query);
            
            // assert
            Assert.Equal("foo fighters", actualResult.Artist.Name);
            Assert.Equal(2019, actualResult.GetYear());
        }
        
        [Fact]
        public void ParseQuery_ArtistYearCity_ValidSetlist()
        {
            // arrange
            var query = "foo fighters 2018 nurburg";
            
            // act
            var actualResult = Util.ParseQuery(query);
            
            // assert
            Assert.Equal("foo fighters", actualResult.Artist.Name);
            Assert.Equal("nurburg", actualResult.Venue.City.Name);
            Assert.Equal(2018, actualResult.GetYear());
        }
    }
}