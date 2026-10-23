import {
  ConflictException,
  Injectable,
  NotFoundException,
} from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import * as bcrypt from 'bcrypt';
import { Raw, Repository } from 'typeorm';

import { User } from './entities/user.entity.js';
import { Role } from '../roles/entities/role.entity.js';
import { CreateUserDto } from './dto/create-user.dto.js';
import { UpdateUserDto } from './dto/update-user.dto.js';

export interface UserResponse {
  id: string;
  name: string;
  username: string;
  role: string;
  isActive: boolean;
  createdAt: Date;
  lastAccessAt: Date | null;
}

@Injectable()
export class UsersService {
  constructor(
    @InjectRepository(User)
    private readonly usersRepository: Repository<User>,

    @InjectRepository(Role)
    private readonly rolesRepository: Repository<Role>,
  ) {}

  async findAll(): Promise<UserResponse[]> {
    const users = await this.usersRepository.find({
      relations: {
        role: {
          permissions: true,
        },
      },
      order: {
        name: 'ASC',
      },
    });

    return users.map((user) => this.toResponse(user));
  }

  async findById(id: string): Promise<User | null> {
    return this.usersRepository.findOne({
      where: { id },
      relations: {
        role: {
          permissions: true,
        },
      },
    });
  }

  async getById(id: string): Promise<UserResponse> {
    const user = await this.getRequiredUser(id);
    return this.toResponse(user);
  }

  async findByEmail(email: string): Promise<User | null> {
    return this.usersRepository.findOne({
      where: {
        email: email.trim().toLowerCase(),
      },
      relations: {
        role: {
          permissions: true,
        },
      },
    });
  }

  findByUsername(username: string): Promise<User | null> {
    return this.findByEmail(username);
  }

  findByName(name: string): Promise<User[]> {
    const normalized = name.trim().toLowerCase();

    return this.usersRepository.find({
      where: {
        name: Raw(
          (alias) => `LOWER(${alias}) = :name`,
          { name: normalized },
        ),
      },
      relations: {
        role: {
          permissions: true,
        },
      },
      take: 2,
    });
  }

  async create(dto: CreateUserDto): Promise<UserResponse> {
    const username = this.normalizeUsername(dto.username);

    await this.ensureUniqueUsername(username);

    const role = await this.getRequiredRole(dto.role);

    const passwordHash = await bcrypt.hash(dto.password, 12);

    const user = this.usersRepository.create({
      name: dto.name.trim(),
      email: username,
      passwordHash,
      role,
      isActive: true,
      lastAccessAt: null,
    });

    const saved = await this.usersRepository.save(user);

    return this.toResponse(saved);
  }

  async updateUser(
    id: string,
    dto: UpdateUserDto,
  ): Promise<UserResponse> {
    const user = await this.getRequiredUser(id);
    const username = this.normalizeUsername(dto.username);

    await this.ensureUniqueUsername(username, id);

    user.name = dto.name.trim();
    user.email = username;

    const saved = await this.usersRepository.save(user);

    return this.toResponse(saved);
  }

  async changeStatus(
    id: string,
    isActive: boolean,
    actorId: string,
  ): Promise<UserResponse> {
    const user = await this.getRequiredUser(id);

    if (!isActive && id === actorId) {
      throw new ConflictException(
        'No puedes desactivar tu propia cuenta administrativa.',
      );
    }

    if (
      user.role.name === 'Administrator' &&
      user.isActive &&
      !isActive
    ) {
      await this.ensureAnotherAdministratorExists(user.id);
    }

    user.isActive = isActive;

    const saved = await this.usersRepository.save(user);

    return this.toResponse(saved);
  }

  async changeRole(
    id: string,
    roleName: string,
    actorId: string,
  ): Promise<UserResponse> {
    const user = await this.getRequiredUser(id);
    const newRole = await this.getRequiredRole(roleName);

    if (
      id === actorId &&
      newRole.name !== 'Administrator'
    ) {
      throw new ConflictException(
        'No puedes quitar el rol Administrator de tu propia cuenta.',
      );
    }

    if (
      user.role.name === 'Administrator' &&
      user.isActive &&
      newRole.name !== 'Administrator'
    ) {
      await this.ensureAnotherAdministratorExists(user.id);
    }

    user.role = newRole;

    const saved = await this.usersRepository.save(user);

    return this.toResponse(saved);
  }

  async update(
    id: string,
    userData: Partial<User>,
  ): Promise<User | null> {
    await this.usersRepository.update(id, userData);

    return this.findById(id);
  }

  private async getRequiredUser(id: string): Promise<User> {
    const user = await this.usersRepository.findOne({
      where: { id },
      relations: {
        role: {
          permissions: true,
        },
      },
    });

    if (!user) {
      throw new NotFoundException(
        'No se encontró el usuario.',
      );
    }

    return user;
  }

  private async getRequiredRole(name: string): Promise<Role> {
    const validRoles = [
      'Administrator',
      'Operator',
      'ConsultationUser',
    ];

    if (!validRoles.includes(name)) {
      throw new ConflictException(
        'Selecciona un rol válido: Administrator, Operator o ConsultationUser.',
      );
    }

    const role = await this.rolesRepository.findOne({
      where: { name },
      relations: {
        permissions: true,
      },
    });

    if (!role) {
      throw new NotFoundException(
        `No existe el rol ${name}.`,
      );
    }

    return role;
  }

  private async ensureUniqueUsername(
    username: string,
    exceptId?: string,
  ): Promise<void> {
    const existing = await this.usersRepository.findOne({
      where: {
        email: username,
      },
    });

    if (existing && existing.id !== exceptId) {
      throw new ConflictException(
        'El login ya está registrado.',
      );
    }
  }

  private async ensureAnotherAdministratorExists(
    excludedUserId: string,
  ): Promise<void> {
    const count = await this.usersRepository
      .createQueryBuilder('user')
      .innerJoin('user.role', 'role')
      .where('user.id != :excludedUserId', {
        excludedUserId,
      })
      .andWhere('user.isActive = true')
      .andWhere('role.name = :role', {
        role: 'Administrator',
      })
      .getCount();

    if (count === 0) {
      throw new ConflictException(
        'Debe permanecer al menos un administrador activo.',
      );
    }
  }

  private normalizeUsername(username: string): string {
    return username.trim().toLowerCase();
  }

  private toResponse(user: User): UserResponse {
    return {
      id: user.id,
      name: user.name,
      username: user.email,
      role: user.role.name,
      isActive: user.isActive,
      createdAt: user.createdAt,
      lastAccessAt: user.lastAccessAt,
    };
  }
}
