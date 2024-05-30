using System.Text.Json;
using Dapper;
using Dometrain.Monolith.Api.Database;
using StackExchange.Redis;

namespace Dometrain.Monolith.Api.Courses;

public interface ICourseRepository
{
    Task<Course?> CreateAsync(Course course);
    
    Task<Course?> GetByIdAsync(Guid id);
    
    Task<Course?> GetBySlugAsync(string slug);
    
    Task<IEnumerable<Course>> GetAllAsync(string nameFilter, int pageNumber, int pageSize);
    
    Task<Course?> UpdateAsync(Course course);
    
    Task<bool> DeleteAsync(Guid id);
}

public class CourseRepository : ICourseRepository
{
    private readonly IDbConnectionFactory _dbConnectionFactory;

    public CourseRepository(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task<Course?> CreateAsync(Course course)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        var result = await connection.ExecuteAsync(
            """
            insert into courses (id, name, description, slug, author)
            values (@id, @name, @description, @slug, @author)
            """, course);
        
        return result > 0 ? course : null;
    }

    public async Task<Course?> GetByIdAsync(Guid id)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        return await connection.QuerySingleOrDefaultAsync<Course>(
            "select * from courses where id = @id", new { id });
    }

    public async Task<Course?> GetBySlugAsync(string slug)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        return await connection.QuerySingleOrDefaultAsync<Course>(
            "select * from courses where slug = @slug", new { slug });
    }

    public async Task<IEnumerable<Course>> GetAllAsync(string nameFilter, int pageNumber, int pageSize)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        return await connection.QueryAsync<Course>(
            """
            select * from courses
                     where (@nameFilter is null or name ilike ('%' || @nameFilter || '%'))
                     limit @pageSize offset @pageOffset
            """, 
            new { nameFilter, pageSize, pageOffset = (pageNumber - 1) * pageSize });
    }

    public async Task<Course?> UpdateAsync(Course course)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        var result = await connection.ExecuteAsync(
            """
            update courses set name = @Name, description = @Description, slug = @Slug, author = @Author
            where id = @Id
            """, course);
        return result > 0 ? course : null;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        var result = await connection.ExecuteAsync(
            "delete from courses where id = @id", new { id });
        return result > 0;
    }
}


public class CachedCourseRepository : ICourseRepository
{
    private readonly ICourseRepository _courseRepository;
    private readonly IConnectionMultiplexer _connectionMultiplexer;

    public CachedCourseRepository(ICourseRepository courseRepository, IConnectionMultiplexer connectionMultiplexer)
    {
        _courseRepository = courseRepository;
        _connectionMultiplexer = connectionMultiplexer;
    }

    public async Task<Course?> CreateAsync(Course course)
    {
        var created = await _courseRepository.CreateAsync(course);
        if (created is null)
        {
            return created;
        }

        var db = _connectionMultiplexer.GetDatabase();
        var serializedCourse = JsonSerializer.Serialize(course);

        var batch = new KeyValuePair<RedisKey, RedisValue>[]
        {
            new($"course_id_{course.Id}", serializedCourse),
            new($"course_slug_{course.Slug}", course.Id.ToString()),
        };
        await db.StringSetAsync(batch);
        return created;
    }

    public async Task<Course?> GetByIdAsync(Guid id)
    {
        var db = _connectionMultiplexer.GetDatabase();
        var cachedCourse = await db.StringGetAsync($"course_id_{id}");
        if (!cachedCourse.IsNull)
        {
            return JsonSerializer.Deserialize<Course>(cachedCourse.ToString());
        }

        var course = await _courseRepository.GetByIdAsync(id);
        if (course is null)
        {
            return course;
        }
        
        var serializedCourse = JsonSerializer.Serialize(course);
        var batch = new KeyValuePair<RedisKey, RedisValue>[]
        {
            new($"course_id_{course.Id}", serializedCourse),
            new($"course_slug_{course.Slug}", course.Id.ToString())
        };
        await db.StringSetAsync(batch);
        return course;
    }

    public async Task<Course?> GetBySlugAsync(string slug)
    {
        var db = _connectionMultiplexer.GetDatabase();
        var cachedCourseKey = await db.StringGetAsync($"course_slug_{slug}");
    
        if (!cachedCourseKey.IsNull)
        {
            return await GetByIdAsync(Guid.Parse(cachedCourseKey.ToString()));
        }
    
        var course = await _courseRepository.GetBySlugAsync(slug);
        if (course is null)
        {
            return course;
        }
        var serializedCourse = JsonSerializer.Serialize(course);
        var batch = new KeyValuePair<RedisKey, RedisValue>[]
        {
            new($"course_id_{course.Id}", serializedCourse),
            new($"course_slug_{course.Slug}", course.Id.ToString())
        };
        await db.StringSetAsync(batch);
        return course;
    }

    public Task<IEnumerable<Course>> GetAllAsync(string nameFilter, int pageNumber, int pageSize)
    {
        return _courseRepository.GetAllAsync(nameFilter, pageNumber, pageSize);
    }

    public async Task<Course?> UpdateAsync(Course course)
    {
        var updated = await _courseRepository.UpdateAsync(course);
        if (updated is null)
        {
            return updated;
        }
        
        var db = _connectionMultiplexer.GetDatabase();
        var serializedCourse = JsonSerializer.Serialize(course);
        var batch = new KeyValuePair<RedisKey, RedisValue>[]
        {
            new($"course_id_{course.Id}", serializedCourse),
            new($"course_slug_{course.Slug}", course.Id.ToString())
        };
        await db.StringSetAsync(batch);
        return updated;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var deleted = await _courseRepository.DeleteAsync(id);

        if (!deleted)
        {
            return deleted;
        }
    
        var db = _connectionMultiplexer.GetDatabase();
        var cachedCourseString = await db.StringGetAsync($"course_id_{id}");
        if (cachedCourseString.IsNull)
        {
            return deleted;
        }
        var course = JsonSerializer.Deserialize<Course>(cachedCourseString!)!;
        var deletedCache = await db.KeyDeleteAsync([$"course_id_{id}",$"course_slug_{course.Slug}"]);
        
        return deletedCache > 0;
    }
}
